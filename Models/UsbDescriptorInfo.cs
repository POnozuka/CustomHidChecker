using System;

namespace CustomHidChecker.Models
{
    public sealed class UsbDescriptorInfo
    {
        public UsbDescriptorInfo(
            string deviceDescriptor,
            string configurationDescriptor,
            string interfaceDescriptor,
            string endpointDescriptor,
            string hidDescriptor,
            string reportDescriptor)
        {
            DeviceDescriptor = deviceDescriptor ?? throw new ArgumentNullException(nameof(deviceDescriptor));
            ConfigurationDescriptor = configurationDescriptor ?? throw new ArgumentNullException(nameof(configurationDescriptor));
            InterfaceDescriptor = interfaceDescriptor ?? throw new ArgumentNullException(nameof(interfaceDescriptor));
            EndpointDescriptor = endpointDescriptor ?? throw new ArgumentNullException(nameof(endpointDescriptor));
            HidDescriptor = hidDescriptor ?? throw new ArgumentNullException(nameof(hidDescriptor));
            ReportDescriptor = reportDescriptor ?? throw new ArgumentNullException(nameof(reportDescriptor));
        }

        public static UsbDescriptorInfo Empty { get; } = new UsbDescriptorInfo(
            "未取得",
            "未取得",
            "未取得",
            "未取得",
            "未取得",
            "未取得");

        public string DeviceDescriptor { get; }

        public string ConfigurationDescriptor { get; }

        public string InterfaceDescriptor { get; }

        public string EndpointDescriptor { get; }

        public string HidDescriptor { get; }

        public string ReportDescriptor { get; }
    }
}
