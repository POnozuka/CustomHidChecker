using CustomHidChecker.Models;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using static CustomHidChecker.Services.HidNativeMethods;

namespace CustomHidChecker.Services
{
    public static class HidEnumerator
    {
        private const int ErrorNoMoreItems = 259;

        public static IReadOnlyList<HidDeviceInfo> Enumerate()
        {
            var hidGuid = HidNativeMethods.HidClassGuid;
            var deviceInfoSet = GetDeviceInfoSet(hidGuid);
            if (deviceInfoSet == IntPtr.Zero || deviceInfoSet.ToInt64() == -1)
            {
                return new List<HidDeviceInfo>();
            }

            var devices = new List<HidDeviceInfo>();
            try
            {
                devices = EnumerateInterfaces(deviceInfoSet, hidGuid);
            }
            finally
            {
                HidNativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return devices;
        }

        private static IntPtr GetDeviceInfoSet(Guid hidGuid)
        {
            return HidNativeMethods.SetupDiGetClassDevs(
                ref hidGuid,
                null,
                IntPtr.Zero,
                HidNativeMethods.SetupDiGetClassDevsFlags.Present |
                HidNativeMethods.SetupDiGetClassDevsFlags.DeviceInterface);
        }

        private static List<HidDeviceInfo> EnumerateInterfaces(IntPtr deviceInfoSet, Guid hidGuid)
        {
            var devices = new List<HidDeviceInfo>();

            for (var index = 0; ; index++)
            {
                var deviceInterfaceData = new HidNativeMethods.SpDeviceInterfaceData
                {
                    Size = Marshal.SizeOf<HidNativeMethods.SpDeviceInterfaceData>()
                };

                if (!HidNativeMethods.SetupDiEnumDeviceInterfaces(
                        deviceInfoSet,
                        IntPtr.Zero,
                        ref hidGuid,
                        index,
                        ref deviceInterfaceData))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == ErrorNoMoreItems)
                    {
                        break;
                    }

                    throw new Win32Exception(error);
                }

                var device = CreateDeviceInfo(deviceInfoSet, ref deviceInterfaceData, index);
                if (device != null)
                {
                    if (devices.Any(d => d.SerialNumber == device.SerialNumber))
                        continue;

                    devices.Add(device);
                }
            }

            return devices;
        }

        private static bool TryGetDevicePath(IntPtr deviceInfoSet, ref HidNativeMethods.SpDeviceInterfaceData deviceInterfaceData, out string? devicePath)
        {
            devicePath = null;

            var requiredSize = 0;
            HidNativeMethods.SetupDiGetDeviceInterfaceDetail(
                deviceInfoSet,
                ref deviceInterfaceData,
                IntPtr.Zero,
                0,
                out requiredSize,
                IntPtr.Zero);

            var detailData = new HidNativeMethods.SpDeviceInterfaceDetailData
            {
                Size = IntPtr.Size == 8 ? 8 : 4 + Marshal.SystemDefaultCharSize
            };

            if (!HidNativeMethods.SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet,
                    ref deviceInterfaceData,
                    ref detailData,
                    Marshal.SizeOf(detailData),
                    out _,
                    IntPtr.Zero))
            {
                return false;
            }

            devicePath = detailData.DevicePath;
            return true;
        }
        private static HidDeviceInfo? CreateDeviceInfo(
            IntPtr deviceInfoSet,
            ref HidNativeMethods.SpDeviceInterfaceData deviceInterfaceData,
            int index)
        {
            // 1. デバイスパス
            if (!TryGetDevicePath(deviceInfoSet, ref deviceInterfaceData, out var devicePath))
            {
                return null;
            }

            // 2. デバイス情報（SP_DEVINFO_DATA）を列挙して InstanceId を取る
            string? instanceId = null;
            {
                var devInfoData = new HidNativeMethods.SP_DEVINFO_DATA
                {
                    cbSize = Marshal.SizeOf<HidNativeMethods.SP_DEVINFO_DATA>()
                };

                // index番目のデバイス情報を取得
                if (HidNativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, index, ref devInfoData))
                {
                    var sb = new StringBuilder(256);
                    int required;
                    if (HidNativeMethods.SetupDiGetDeviceInstanceId(
                            deviceInfoSet,
                            ref devInfoData,
                            sb,
                            sb.Capacity,
                            out required))
                    {
                        instanceId = sb.ToString();
                    }
                }
            }

            try
            {
                // 3. ハンドルを開く
                using var handle = HidNativeMethods.CreateFileForReadWrite(devicePath);
                if (handle.IsInvalid)
                {
                    return null;
                }

                // 4. ベーシック属性
                var attributes = new HidNativeMethods.HidAttributes
                {
                    Size = Marshal.SizeOf<HidNativeMethods.HidAttributes>()
                };

                if (!HidNativeMethods.HidD_GetAttributes(handle, ref attributes))
                {
                    return null;
                }

                // 5. キャパビリティ
                if (!HidNativeMethods.TryGetCapabilities(handle, out var caps))
                {
                    return null;
                }

                // 6. 表示用情報
                var productName = HidNativeMethods.GetStringProperty(handle, HidNativeMethods.HidD_GetProductString);
                var manufacturerName = HidNativeMethods.GetStringProperty(handle, HidNativeMethods.HidD_GetManufacturerString);

                // 7. シリアル番号 (ある場合)
                var serialNumber = HidNativeMethods.GetStringProperty(handle, HidNativeMethods.HidD_GetSerialNumberString);

                // 8. HidDeviceInfo を組み立てる（新フィールドも含む）
                return new HidDeviceInfo(
                    devicePath,
                    attributes.VendorID,
                    attributes.ProductID,
                    attributes.VersionNumber,
                    caps.InputReportByteLength,
                    caps.OutputReportByteLength,
                    caps.FeatureReportByteLength,
                    string.IsNullOrWhiteSpace(productName) ? null : productName,
                    string.IsNullOrWhiteSpace(manufacturerName) ? null : manufacturerName,
                    string.IsNullOrWhiteSpace(instanceId) ? null : instanceId,
                    string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber
                );
            }
            catch
            {
                return null;
            }
        }
    }
}