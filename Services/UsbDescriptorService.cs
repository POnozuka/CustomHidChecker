using System;
using System.Runtime.InteropServices;
using System.Text;
using CustomHidChecker.Models;
using Microsoft.Win32.SafeHandles;

namespace CustomHidChecker.Services
{
    public sealed class UsbDescriptorService
    {
        public UsbDescriptorInfo GetDescriptors(HidDeviceInfo info)
        {
            string deviceDescriptor;
            var configurationDescriptor = "未取得";
            var interfaceDescriptor = "未取得";
            var endpointDescriptor = "未取得";

            try
            {
                using var handle = HidNativeMethods.CreateFileForReadWrite(info.DevicePath);
                deviceDescriptor = GetDeviceDescriptorText(info, handle);

                if (HidNativeMethods.TryGetCapabilities(handle, out var caps))
                {
                    configurationDescriptor = GetConfigurationDescriptorText(caps);
                    interfaceDescriptor = GetInterfaceDescriptorText(caps);
                    endpointDescriptor = GetEndpointDescriptorText(caps);
                }
                else
                {
                    configurationDescriptor = "取得失敗: Capabilities取得不可";
                    interfaceDescriptor = configurationDescriptor;
                    endpointDescriptor = configurationDescriptor;
                }
            }
            catch (Exception ex)
            {
                var failure = $"取得失敗: {ex.Message}";
                deviceDescriptor = GetDeviceDescriptorText(info);
                configurationDescriptor = failure;
                interfaceDescriptor = failure;
                endpointDescriptor = failure;
            }

            return new UsbDescriptorInfo(
                deviceDescriptor,
                configurationDescriptor,
                interfaceDescriptor,
                endpointDescriptor);
        }

        private static string GetDeviceDescriptorText(HidDeviceInfo info, SafeFileHandle? handle = null)
        {
            HidNativeMethods.HidAttributes? attributes = null;

            if (handle is not null)
            {
                var nativeAttributes = new HidNativeMethods.HidAttributes
                {
                    Size = Marshal.SizeOf<HidNativeMethods.HidAttributes>()
                };

                if (HidNativeMethods.HidD_GetAttributes(handle, ref nativeAttributes))
                {
                    attributes = nativeAttributes;
                }
            }

            var builder = new StringBuilder();
            var vendorId = attributes?.VendorID ?? info.VendorId;
            var productId = attributes?.ProductID ?? info.ProductId;
            var versionNumber = attributes?.VersionNumber ?? info.VersionNumber;
            builder.AppendLine($"Vendor ID: 0x{vendorId:X4}");
            builder.AppendLine($"Product ID: 0x{productId:X4}");
            builder.AppendLine($"Release Number: 0x{versionNumber:X4}");
            builder.AppendLine($"Device Path: {info.DevicePath}");
            builder.AppendLine($"Input Report Length: {Math.Max(info.InputReportLength, 0)} bytes");
            builder.AppendLine($"Output Report Length: {Math.Max(info.OutputReportLength, 0)} bytes");
            builder.AppendLine($"Feature Report Length: {Math.Max(info.FeatureReportLength, 0)} bytes");

            if (!string.IsNullOrWhiteSpace(info.ManufacturerName))
            {
                builder.AppendLine($"Vendor Name: {info.ManufacturerName}");
            }

            if (!string.IsNullOrWhiteSpace(info.ProductName))
            {
                builder.AppendLine($"Product Name: {info.ProductName}");
            }

            if (!string.IsNullOrWhiteSpace(info.InstanceId))
            {
                builder.AppendLine($"Instance ID: {info.InstanceId}");
            }

            if (!string.IsNullOrWhiteSpace(info.SerialNumber))
            {
                builder.AppendLine($"Serial Number: {info.SerialNumber}");
            }

            return builder.ToString().TrimEnd();
        }

        private static string GetConfigurationDescriptorText(HidNativeMethods.HidP_Caps caps)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Number of Configurations: 1");
            builder.AppendLine("Power Requirements: Unknown (not available via HID class API)");
            builder.AppendLine($"Input Report Length: {Math.Max(caps.InputReportByteLength, (short)0)} bytes");
            builder.AppendLine($"Output Report Length: {Math.Max(caps.OutputReportByteLength, (short)0)} bytes");
            builder.AppendLine($"Feature Report Length: {Math.Max(caps.FeatureReportByteLength, (short)0)} bytes");
            return builder.ToString();
        }

        private static string GetInterfaceDescriptorText(HidNativeMethods.HidP_Caps caps)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Usage Page: 0x{(ushort)caps.UsagePage:X4}");
            builder.AppendLine($"Usage: 0x{(ushort)caps.Usage:X4}");
            builder.AppendLine($"Number of Input Button Caps: {caps.NumberInputButtonCaps}");
            builder.AppendLine($"Number of Input Value Caps: {caps.NumberInputValueCaps}");
            builder.AppendLine($"Number of Output Button Caps: {caps.NumberOutputButtonCaps}");
            builder.AppendLine($"Number of Output Value Caps: {caps.NumberOutputValueCaps}");
            builder.AppendLine($"Number of Feature Button Caps: {caps.NumberFeatureButtonCaps}");
            builder.AppendLine($"Number of Feature Value Caps: {caps.NumberFeatureValueCaps}");
            return builder.ToString();
        }

        private static string GetEndpointDescriptorText(HidNativeMethods.HidP_Caps caps)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"IN Endpoint: {(caps.InputReportByteLength > 0 ? $"Interrupt / Report Length {caps.InputReportByteLength} bytes" : "None")}");
            builder.AppendLine($"OUT Endpoint: {(caps.OutputReportByteLength > 0 ? $"Interrupt / Report Length {caps.OutputReportByteLength} bytes" : "None")}");
            builder.AppendLine($"Feature Report: {(caps.FeatureReportByteLength > 0 ? $"Report Length {caps.FeatureReportByteLength} bytes" : "None")}");
            builder.AppendLine("Polling Interval: Unknown (not available via HID class API)");
            return builder.ToString();
        }
    }
}
