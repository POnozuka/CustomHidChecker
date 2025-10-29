using System;
using System.Runtime.InteropServices;
using System.Text;
using CustomHidChecker.Models;

namespace CustomHidChecker.Services
{
    public sealed class UsbDescriptorService
    {
        public UsbDescriptorInfo GetDescriptors(HidDeviceInfo info)
        {
            var deviceDescriptor = BuildDeviceDescriptor(info);
            var configurationDescriptor = "未取得";
            var interfaceDescriptor = "未取得";
            var endpointDescriptor = "未取得";

            try
            {
                using var handle = HidNativeMethods.CreateFileForReadWrite(info.DevicePath);

                var attributes = new HidNativeMethods.HidAttributes
                {
                    Size = Marshal.SizeOf<HidNativeMethods.HidAttributes>()
                };


                if (HidNativeMethods.HidD_GetAttributes(handle, ref attributes))
                {
                    deviceDescriptor = BuildDeviceDescriptor(info, attributes);
                }

                if (HidNativeMethods.TryGetCapabilities(handle, out var caps))
                {
                    configurationDescriptor = BuildConfigurationDescriptor(caps);
                    interfaceDescriptor = BuildInterfaceDescriptor(caps);
                    endpointDescriptor = BuildEndpointDescriptor(caps);
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

        private static string BuildDeviceDescriptor(HidDeviceInfo info)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"ベンダID: 0x{info.VendorId:X4}");
            builder.AppendLine($"プロダクトID: 0x{info.ProductId:X4}");
            builder.AppendLine($"リリース番号: 0x{info.VersionNumber:X4}");
            builder.AppendLine($"デバイスパス: {info.DevicePath}");

            if (!string.IsNullOrWhiteSpace(info.ManufacturerName))
            {
                builder.AppendLine($"ベンダ名: {info.ManufacturerName}");
            }

            if (!string.IsNullOrWhiteSpace(info.ProductName))
            {
                builder.AppendLine($"製品名: {info.ProductName}");
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildDeviceDescriptor(HidDeviceInfo info, HidNativeMethods.HidAttributes attributes)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"ベンダID: 0x{attributes.VendorID:X4}");
            builder.AppendLine($"プロダクトID: 0x{attributes.ProductID:X4}");
            builder.AppendLine($"リリース番号: 0x{attributes.VersionNumber:X4}");
            builder.AppendLine($"デバイスパス: {info.DevicePath}");

            if (!string.IsNullOrWhiteSpace(info.ManufacturerName))
            {
                builder.AppendLine($"ベンダ名: {info.ManufacturerName}");
            }

            if (!string.IsNullOrWhiteSpace(info.ProductName))
            {
                builder.AppendLine($"製品名: {info.ProductName}");
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildConfigurationDescriptor(HidNativeMethods.HidP_Caps caps)
        {
            var builder = new StringBuilder();
            builder.AppendLine("構成数: 1");
            builder.AppendLine("電源要件: 不明 (HIDクラスAPIでは取得不可)");
            builder.AppendLine($"Input Report 長: {Math.Max(caps.InputReportByteLength, (short)0)} バイト");
            builder.AppendLine($"Output Report 長: {Math.Max(caps.OutputReportByteLength, (short)0)} バイト");
            builder.AppendLine($"Feature Report 長: {Math.Max(caps.FeatureReportByteLength, (short)0)} バイト");
            return builder.ToString();
        }

        private static string BuildInterfaceDescriptor(HidNativeMethods.HidP_Caps caps)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Usage Page: 0x{(ushort)caps.UsagePage:X4}");
            builder.AppendLine($"Usage: 0x{(ushort)caps.Usage:X4}");
            builder.AppendLine($"入力ボタンCAP数: {caps.NumberInputButtonCaps}");
            builder.AppendLine($"入力値CAP数: {caps.NumberInputValueCaps}");
            builder.AppendLine($"出力ボタンCAP数: {caps.NumberOutputButtonCaps}");
            builder.AppendLine($"出力値CAP数: {caps.NumberOutputValueCaps}");
            builder.AppendLine($"FeatureボタンCAP数: {caps.NumberFeatureButtonCaps}");
            builder.AppendLine($"Feature値CAP数: {caps.NumberFeatureValueCaps}");
            return builder.ToString();
        }

        private static string BuildEndpointDescriptor(HidNativeMethods.HidP_Caps caps)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"INエンドポイント: {(caps.InputReportByteLength > 0 ? $"Interrupt / Report長 {caps.InputReportByteLength} バイト" : "なし")}");
            builder.AppendLine($"OUTエンドポイント: {(caps.OutputReportByteLength > 0 ? $"Interrupt / Report長 {caps.OutputReportByteLength} バイト" : "なし")}");
            builder.AppendLine($"Featureパス: {(caps.FeatureReportByteLength > 0 ? $"Report長 {caps.FeatureReportByteLength} バイト" : "なし")}");
            builder.AppendLine("ポーリング間隔: 不明 (HIDクラスAPIでは取得不可)");
            return builder.ToString();
        }
    }
}
