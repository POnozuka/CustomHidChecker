using System;
using CustomHidChecker.Services;

namespace CustomHidChecker.Models
{
    public enum DeviceLogDirection
    {
        Info,
        Output,
        Input,
        FeatureIn,
        FeatureOut,
        Error,
    }

    public sealed class DeviceLogEntry
    {
        public DeviceLogEntry(DeviceLogDirection direction, string message, byte[]? payload = null)
        {
            Timestamp = DateTimeOffset.Now;
            Direction = direction;
            Message = message;
            Payload = payload ?? Array.Empty<byte>();
        }

        public DateTimeOffset Timestamp { get; }

        public DeviceLogDirection Direction { get; }

        public string Message { get; }

        public byte[] Payload { get; }

        public string PayloadHex => HexConverter.ToHexString(Payload);

        public string DirectionLabel => Direction switch
        {
            DeviceLogDirection.Info => "INFO",
            DeviceLogDirection.Output => "OUT",
            DeviceLogDirection.Input => "IN",
            DeviceLogDirection.FeatureIn => "FEATURE IN",
            DeviceLogDirection.FeatureOut => "FEATURE OUT",
            DeviceLogDirection.Error => "ERROR",
            _ => Direction.ToString().ToUpperInvariant()
        };
    }
}
