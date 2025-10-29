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
            short topLevelCollectionCount,
            string? productName,
            string? manufacturerName,
            string? instanceId,
            string? serialNumber,
            int topLevelCollectionIndex,
            int topLevelCollectionTotal)
        {
            DevicePath = devicePath ?? throw new ArgumentNullException(nameof(devicePath));
            VendorId = vendorId;
            ProductId = productId;
            VersionNumber = versionNumber;
            InputReportLength = inputReportLength;
            OutputReportLength = outputReportLength;
            FeatureReportLength = featureReportLength;
            TopLevelCollectionCount = topLevelCollectionCount;
            ProductName = productName;
            ManufacturerName = manufacturerName;
            InstanceId = instanceId;
            SerialNumber = serialNumber;
            TopLevelCollectionIndex = topLevelCollectionIndex;
            TopLevelCollectionTotal = topLevelCollectionTotal;
        }

        public string DevicePath { get; }

        public ushort VendorId { get; }

        public ushort ProductId { get; }

        public ushort VersionNumber { get; }

        public int InputReportLength { get; }

        public int OutputReportLength { get; }

        public int FeatureReportLength { get; }

        public short TopLevelCollectionCount { get; }

        public int TopLevelCollectionIndex { get; }

        public int TopLevelCollectionTotal { get; }

        public string? ProductName { get; }

        public string? ManufacturerName { get; }

        public string? InstanceId { get; }

        public string? SerialNumber { get; }


        public string DisplayName
        {
            get
            {
                var baseName = string.IsNullOrWhiteSpace(ProductName)
                    ? $"VID:0x{VendorId:X4}, PID:0x{ProductId:X4}"
                    : $"{ProductName} (VID:0x{VendorId:X4}, PID:0x{ProductId:X4})";

                return TopLevelCollectionIndex > 1
                    ? $"{baseName} [TLC #{TopLevelCollectionIndex}]"
                    : baseName;
            }
        }
    }
}
