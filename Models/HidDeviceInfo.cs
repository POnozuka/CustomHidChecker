using System;

namespace CustomHidChecker.Models
{
    public sealed class HidDeviceInfo
    {
        public HidDeviceInfo(
            string devicePath,
            ushort vendorId,
            ushort productId,
            ushort versionNumber,
            int inputReportLength,
            int outputReportLength,
            int featureReportLength,
            string? productName,
            string? manufacturerName)
        {
            DevicePath = devicePath ?? throw new ArgumentNullException(nameof(devicePath));
            VendorId = vendorId;
            ProductId = productId;
            VersionNumber = versionNumber;
            InputReportLength = inputReportLength;
            OutputReportLength = outputReportLength;
            FeatureReportLength = featureReportLength;
            ProductName = productName;
            ManufacturerName = manufacturerName;
        }

        public string DevicePath { get; }

        public ushort VendorId { get; }

        public ushort ProductId { get; }

        public ushort VersionNumber { get; }

        public int InputReportLength { get; }

        public int OutputReportLength { get; }

        public int FeatureReportLength { get; }

        public string? ProductName { get; }

        public string? ManufacturerName { get; }

        public string DisplayName => string.IsNullOrWhiteSpace(ProductName)
            ? $"VID:0x{VendorId:X4}, PID:0x{ProductId:X4}"
            : $"{ProductName} (VID:0x{VendorId:X4}, PID:0x{ProductId:X4})";
    }
}
