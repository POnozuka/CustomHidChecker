using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using CustomHidChecker.Models;
using CustomHidChecker.ViewModels;

namespace CustomHidChecker.Services
{
    public sealed class DeviceLogService
    {
        private readonly ObservableCollection<LogEntryViewModel> _entries = new();
        private readonly object _lock = new();

        public ObservableCollection<LogEntryViewModel> Entries => _entries;

        public bool HasEntries => _entries.Any();

        public void Add(DeviceLogDirection direction, string message, byte[]? payload = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lock)
                {
                    if (_entries.Count > 1000)
                    {
                        _entries.RemoveAt(0);
                    }

                    _entries.Add(new LogEntryViewModel(new DeviceLogEntry(direction, message, payload)));
                }
            });
        }

        public void Clear()
        {
            Application.Current.Dispatcher.Invoke(_entries.Clear);
        }

        public bool TryExport(string filePath, out string? errorMessage)
        {
            try
            {
                using var writer = new StreamWriter(filePath);
                writer.WriteLine("Timestamp,Direction,Message,Payload");
                foreach (var entry in _entries)
                {
                    var line = string.Join(",", new[]
                    {
                        EscapeCsv(entry.Timestamp),
                        EscapeCsv(entry.Direction),
                        EscapeCsv(entry.Message),
                        EscapeCsv(entry.Payload)
                    });
                    writer.WriteLine(line);
                }

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
