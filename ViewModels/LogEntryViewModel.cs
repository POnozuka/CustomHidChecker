using CustomHidChecker.Models;

namespace CustomHidChecker.ViewModels
{
    public sealed class LogEntryViewModel
    {
        public LogEntryViewModel(DeviceLogEntry entry)
        {
            Entry = entry;
        }

        public DeviceLogEntry Entry { get; }

        public string Timestamp => Entry.Timestamp.ToString("HH:mm:ss.fff");

        public string Direction => Entry.DirectionLabel;

        public string Message => Entry.Message;

        public string Payload => Entry.PayloadHex;
    }
}
