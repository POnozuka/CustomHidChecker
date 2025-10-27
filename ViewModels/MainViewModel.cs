using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CustomHidChecker.Commands;
using CustomHidChecker.Interfaces;
using CustomHidChecker.Models;
using CustomHidChecker.Services;
using Microsoft.Win32;

namespace CustomHidChecker.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private readonly ObservableCollection<LogEntryViewModel> _logEntries;
        private readonly ObservableCollection<HidDeviceInfo> _devices;
        private readonly Func<IHidDevice> _deviceFactory;
        private readonly object _logLock = new();
        private IHidDevice? _device;
        private CancellationTokenSource? _readLoopCts;
        private HidDeviceInfo? _selectedDevice;
        private string _outputReportText = string.Empty;
        private string _featureReportText = string.Empty;
        private string _statusMessage = "準備完了";
        private string _connectedDeviceDetails = "未接続";
        private string _lastInputReport = "未受信";
        private string _lastInputReportTimestamp = "-";
        private bool _isConnected;
        private bool _isBusy;

        public MainViewModel()
            : this(() => new WinHidDevice())
        {
        }

        public MainViewModel(Func<IHidDevice> deviceFactory)
        {
            _deviceFactory = deviceFactory;
            _devices = new ObservableCollection<HidDeviceInfo>();
            _logEntries = new ObservableCollection<LogEntryViewModel>();

            RefreshDevicesCommand = new RelayCommand(_ => RefreshDevices(), _ => !IsBusy);
            ConnectCommand = new RelayCommand(_ => ConnectDevice(), _ => !IsBusy && !IsConnected && SelectedDevice != null);
            DisconnectCommand = new RelayCommand(_ => DisconnectDevice(), _ => IsConnected);
            SendOutputCommand = new RelayCommand(_ => SendOutputReport(), _ => IsConnected);
            GetFeatureCommand = new RelayCommand(_ => GetFeatureReport(), _ => IsConnected);
            SetFeatureCommand = new RelayCommand(_ => SetFeatureReport(), _ => IsConnected);
            ExportLogCommand = new RelayCommand(_ => ExportLog(), _ => _logEntries.Any());
            ClearLogCommand = new RelayCommand(_ => ClearLog(), _ => _logEntries.Any());

            RefreshDevices();
        }

        public ObservableCollection<HidDeviceInfo> Devices => _devices;

        public ObservableCollection<LogEntryViewModel> LogEntries => _logEntries;

        public HidDeviceInfo? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public string OutputReportText
        {
            get => _outputReportText;
            set => SetProperty(ref _outputReportText, value);
        }

        public string FeatureReportText
        {
            get => _featureReportText;
            set => SetProperty(ref _featureReportText, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string ConnectedDeviceDetails
        {
            get => _connectedDeviceDetails;
            private set => SetProperty(ref _connectedDeviceDetails, value);
        }

        public string LastInputReport
        {
            get => _lastInputReport;
            private set => SetProperty(ref _lastInputReport, value);
        }

        public string LastInputReportTimestamp
        {
            get => _lastInputReportTimestamp;
            private set => SetProperty(ref _lastInputReportTimestamp, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public RelayCommand RefreshDevicesCommand { get; }

        public RelayCommand ConnectCommand { get; }

        public RelayCommand DisconnectCommand { get; }

        public RelayCommand SendOutputCommand { get; }

        public RelayCommand GetFeatureCommand { get; }

        public RelayCommand SetFeatureCommand { get; }

        public RelayCommand ExportLogCommand { get; }

        public RelayCommand ClearLogCommand { get; }

        private async void RefreshDevices()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "デバイスを検索しています...";
                var devices = await Task.Run(HidEnumerator.Enumerate);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _devices.Clear();
                    foreach (var device in devices)
                    {
                        _devices.Add(device);
                    }

                    if (_devices.Count > 0)
                    {
                        SelectedDevice = _devices[0];
                        StatusMessage = $"{_devices.Count} 台のデバイスを検出";
                    }
                    else
                    {
                        SelectedDevice = null;
                        StatusMessage = "HIDデバイスが見つかりません";
                    }
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"列挙に失敗: {ex.Message}";
                AddLog(DeviceLogDirection.Error, "デバイス列挙エラー", null);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ConnectDevice()
        {
            if (IsConnected || SelectedDevice == null)
            {
                return;
            }

            var deviceInfo = SelectedDevice;
            try
            {
                IsBusy = true;
                StatusMessage = "接続中...";
                _device = _deviceFactory();
                if (!_device.Open(deviceInfo.DevicePath))
                {
                    var message = _device.LastErrorMessage ?? "不明な理由で接続に失敗";
                    StatusMessage = message;
                    AddLog(DeviceLogDirection.Error, message);
                    _device.Dispose();
                    _device = null;
                    return;
                }

                IsConnected = true;
                StatusMessage = $"接続しました: {deviceInfo.DisplayName}";
                AddLog(DeviceLogDirection.Info, "デバイスに接続", null);
                ConnectedDeviceDetails = BuildDeviceDetails(deviceInfo);

                _readLoopCts = new CancellationTokenSource();
                _ = Task.Run(() => ReadLoopAsync(_readLoopCts.Token, deviceInfo.InputReportLength), _readLoopCts.Token);
            }
            catch (Exception ex)
            {
                StatusMessage = $"接続に失敗: {ex.Message}";
                AddLog(DeviceLogDirection.Error, ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ReadLoopAsync(CancellationToken cancellationToken, int reportLength)
        {
            if (_device is null)
            {
                return;
            }

            var length = reportLength > 0 ? reportLength : 64;
            var buffer = new byte[length];

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var memory = buffer.AsMemory();
                    var read = await _device.ReadAsync(memory, 1000, cancellationToken).ConfigureAwait(false);
                    if (read > 0)
                    {
                        var data = memory.Slice(0, read).ToArray();
                        AddLog(DeviceLogDirection.Input, "Input Report受信", data);
                        UpdateLastInputReport(data);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AddLog(DeviceLogDirection.Error, ex.Message);
                    StatusMessage = $"読み取りエラー: {ex.Message}";
                    await Application.Current.Dispatcher.InvokeAsync(DisconnectDevice);
                    break;
                }
            }
        }

        private void DisconnectDevice()
        {
            _readLoopCts?.Cancel();
            _readLoopCts?.Dispose();
            _readLoopCts = null;

            if (_device != null)
            {
                _device.Close();
                _device.Dispose();
                _device = null;
            }

            if (IsConnected)
            {
                AddLog(DeviceLogDirection.Info, "デバイスから切断", null);
            }

            IsConnected = false;
            StatusMessage = "未接続";
            ConnectedDeviceDetails = "未接続";
            LastInputReport = "未受信";
            LastInputReportTimestamp = "-";
        }

        private void SendOutputReport()
        {
            if (_device is null || SelectedDevice is null)
            {
                return;
            }

            var deviceInfo = SelectedDevice;
            var report = BuildReportBuffer(OutputReportText, deviceInfo.OutputReportLength, out var errorMessage);
            if (report is null)
            {
                StatusMessage = errorMessage;
                AddLog(DeviceLogDirection.Error, errorMessage);
                return;
            }

            _ = Task.Run(async () =>
            {
                var success = await _device.WriteAsync(report, CancellationToken.None).ConfigureAwait(false);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (success)
                    {
                        StatusMessage = "Output Report送信完了";
                        AddLog(DeviceLogDirection.Output, "Output Report送信", report);
                    }
                    else
                    {
                        var message = _device.LastErrorMessage ?? "送信に失敗";
                        StatusMessage = message;
                        AddLog(DeviceLogDirection.Error, message);
                    }
                });
            });
        }

        private void GetFeatureReport()
        {
            if (_device is null || SelectedDevice is null)
            {
                return;
            }

            var deviceInfo = SelectedDevice;
            var featureLength = deviceInfo.FeatureReportLength > 0 ? deviceInfo.FeatureReportLength : 64;
            var buffer = new byte[featureLength];

            if (!HexConverter.TryParseHexString(FeatureReportText, buffer, out var written) || written == 0)
            {
                var message = "Feature Report IDを先頭に指定してください";
                StatusMessage = message;
                AddLog(DeviceLogDirection.Error, message);
                return;
            }

            if (!_device.GetFeature(buffer))
            {
                var error = _device.LastErrorMessage ?? "Feature Report取得に失敗";
                StatusMessage = error;
                AddLog(DeviceLogDirection.Error, error);
                return;
            }

            FeatureReportText = HexConverter.ToHexString(buffer);
            StatusMessage = "Feature Report取得完了";
            AddLog(DeviceLogDirection.FeatureIn, "Feature Report受信", buffer);
        }

        private void SetFeatureReport()
        {
            if (_device is null || SelectedDevice is null)
            {
                return;
            }

            var deviceInfo = SelectedDevice;
            var report = BuildReportBuffer(FeatureReportText, deviceInfo.FeatureReportLength > 0 ? deviceInfo.FeatureReportLength : 64, out var errorMessage);
            if (report is null)
            {
                StatusMessage = errorMessage;
                AddLog(DeviceLogDirection.Error, errorMessage);
                return;
            }

            var success = _device.SetFeature(report);
            if (success)
            {
                StatusMessage = "Feature Report送信完了";
                AddLog(DeviceLogDirection.FeatureOut, "Feature Report送信", report);
            }
            else
            {
                var message = _device.LastErrorMessage ?? "Feature Report送信に失敗";
                StatusMessage = message;
                AddLog(DeviceLogDirection.Error, message);
            }
        }

        private void ExportLog()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV ファイル (*.csv)|*.csv|テキスト ファイル (*.txt)|*.txt",
                FileName = $"HidLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                using var writer = new StreamWriter(dialog.FileName);
                writer.WriteLine("Timestamp,Direction,Message,Payload");
                foreach (var entry in _logEntries)
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

                StatusMessage = "ログをエクスポートしました";
            }
            catch (Exception ex)
            {
                StatusMessage = $"エクスポートに失敗: {ex.Message}";
                AddLog(DeviceLogDirection.Error, ex.Message);
            }
        }

        private void ClearLog()
        {
            Application.Current.Dispatcher.Invoke(_logEntries.Clear);
            StatusMessage = "ログをクリアしました";
        }

        private byte[]? BuildReportBuffer(string input, int length, out string errorMessage)
        {
            var effectiveLength = length > 0 ? length : 64;
            var buffer = new byte[effectiveLength];
            if (!HexConverter.TryParseHexString(input, buffer, out var written))
            {
                errorMessage = "HEX形式が不正です";
                return null;
            }

            if (written == 0)
            {
                errorMessage = "Report IDを先頭に指定してください";
                return null;
            }

            errorMessage = string.Empty;
            return buffer;
        }

        private void AddLog(DeviceLogDirection direction, string message, byte[]? payload = null)
        {
            var entry = new LogEntryViewModel(new DeviceLogEntry(direction, message, payload));
            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_logLock)
                {
                    if (_logEntries.Count > 1000)
                    {
                        _logEntries.RemoveAt(0);
                    }

                    _logEntries.Add(entry);
                }

                RaiseCommandStates();
            });
        }

        private void RaiseCommandStates()
        {
            RefreshDevicesCommand.RaiseCanExecuteChanged();
            ConnectCommand.RaiseCanExecuteChanged();
            DisconnectCommand.RaiseCanExecuteChanged();
            SendOutputCommand.RaiseCanExecuteChanged();
            GetFeatureCommand.RaiseCanExecuteChanged();
            SetFeatureCommand.RaiseCanExecuteChanged();
            ExportLogCommand.RaiseCanExecuteChanged();
            ClearLogCommand.RaiseCanExecuteChanged();
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private static string BuildDeviceDetails(HidDeviceInfo info)
        {
            var inputLength = info.InputReportLength > 0 ? info.InputReportLength.ToString() : "不明";
            var outputLength = info.OutputReportLength > 0 ? info.OutputReportLength.ToString() : "不明";
            var featureLength = info.FeatureReportLength > 0 ? info.FeatureReportLength.ToString() : "不明";

            return $"名称: {info.DisplayName}\nVID:0x{info.VendorId:X4} / PID:0x{info.ProductId:X4} / バージョン: 0x{info.VersionNumber:X4}\nInput Report: {inputLength} bytes / Output Report: {outputLength} bytes / Feature Report: {featureLength} bytes";
        }

        private void UpdateLastInputReport(byte[] data)
        {
            var hex = HexConverter.ToHexString(data);
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Application.Current.Dispatcher.Invoke(() =>
            {
                LastInputReport = string.IsNullOrEmpty(hex) ? "(受信データなし)" : hex;
                LastInputReportTimestamp = timestamp;
            });
        }
    }
}
