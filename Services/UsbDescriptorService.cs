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
            var hidDescriptorText = "未取得";
            var reportDescriptorText = "未取得";

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

                    try
                    {
                        var hidDescriptor = HidNativeMethods.GetHidDescriptor(handle);
                        hidDescriptorText = BuildHidDescriptor(hidDescriptor);

                        try
                        {
                            var reportDescriptor = TrimDescriptor(
                                HidNativeMethods.GetReportDescriptor(handle, hidDescriptor.Descriptor0.DescriptorLength),
                                hidDescriptor.Descriptor0.DescriptorLength);
                            reportDescriptorText = BuildReportDescriptor(reportDescriptor);
                        }
                        catch (Exception ex)
                        {
                            reportDescriptorText = $"取得失敗: {ex.Message}";
                        }
                    }
                    catch (Exception ex)
                    {
                        hidDescriptorText = $"取得失敗: {ex.Message}";
                        reportDescriptorText = hidDescriptorText;
                    }
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
                hidDescriptorText = failure;
                reportDescriptorText = failure;
            }

            return new UsbDescriptorInfo(
                deviceDescriptor,
                configurationDescriptor,
                interfaceDescriptor,
                endpointDescriptor,
                hidDescriptorText,
                reportDescriptorText);
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

        private static string BuildHidDescriptor(HidNativeMethods.HidDescriptor descriptor)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"長さ: {descriptor.Length} バイト");
            builder.AppendLine($"タイプ: 0x{descriptor.DescriptorType:X2}");
            builder.AppendLine($"HIDバージョン: {FormatBcd(descriptor.HidSpecification)}");
            builder.AppendLine($"国コード: {descriptor.CountryCode}");
            builder.AppendLine($"レポートディスクリプタ長: {descriptor.Descriptor0.DescriptorLength} バイト");
            return builder.ToString();
        }

        private static string BuildReportDescriptor(byte[] descriptor)
        {
            if (descriptor.Length == 0)
            {
                return "未取得";
            }

            var builder = new StringBuilder();
            for (var offset = 0; offset < descriptor.Length; offset += 16)
            {
                var length = Math.Min(16, descriptor.Length - offset);
                builder.AppendLine($"0x{offset:X4}: {HexConverter.ToHexString(descriptor.AsSpan(offset, length))}");
            }

            return builder.ToString().TrimEnd();
        }

        private static byte[] TrimDescriptor(byte[] descriptor, int expectedLength)
        {
            if (expectedLength > 0 && expectedLength <= descriptor.Length)
            {
                var result = new byte[expectedLength];
                Array.Copy(descriptor, result, expectedLength);
                return result;
            }

            var lastIndex = Array.FindLastIndex(descriptor, b => b != 0);
            if (lastIndex < 0)
            {
                return Array.Empty<byte>();
            }

            var trimmed = new byte[lastIndex + 1];
            Array.Copy(descriptor, trimmed, trimmed.Length);
            return trimmed;
        }

        private static string FormatBcd(ushort value)
        {
            var major = (value & 0xFF00) >> 8;
            var minorHigh = (value & 0x00F0) >> 4;
            var minorLow = value & 0x000F;
            return $"{major}.{minorHigh}{minorLow}";
        }
    }
}
