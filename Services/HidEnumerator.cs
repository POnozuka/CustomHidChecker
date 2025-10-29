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

            foreach (var device in devices)
            {
                Debug.WriteLine(
                    $"HID Device: Path={device.DevicePath}, VID=0x{device.VendorId:X4}, PID=0x{device.ProductId:X4}, Version=0x{device.VersionNumber:X4}, Input={device.InputReportLength}, Output={device.OutputReportLength}, Feature={device.FeatureReportLength}, ProductName={device.ProductName ?? "(null)"}, ManufacturerName={device.ManufacturerName ?? "(null)"}, InstanceId={device.InstanceId ?? "(null)"}, SerialNumber={device.SerialNumber ?? "(null)"}");
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
            // 1. Device path
            if (!TryGetDevicePath(deviceInfoSet, ref deviceInterfaceData, out var devicePath))
            {
                return null;
            }

            // 2. Enumerate device info (SP_DEVINFO_DATA) to obtain the InstanceId
            string? instanceId = null;
            {
                var devInfoData = new HidNativeMethods.SP_DEVINFO_DATA
                {
                    cbSize = Marshal.SizeOf<HidNativeMethods.SP_DEVINFO_DATA>()
                };

                // Retrieve the device info at the specified index
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
                // 3. Open a handle
                using var handle = HidNativeMethods.CreateFileForReadWrite(devicePath);
                if (handle.IsInvalid)
                {
                    return null;
                }

                // 4. Basic attributes
                var attributes = new HidNativeMethods.HidAttributes
                {
                    Size = Marshal.SizeOf<HidNativeMethods.HidAttributes>()
                };

                if (!HidNativeMethods.HidD_GetAttributes(handle, ref attributes))
                {
                    return null;
                }

                // 5. Capabilities
                if (!HidNativeMethods.TryGetCapabilities(handle, out var caps))
                {
                    return null;
                }

                // 6. Display information
                var productName = HidNativeMethods.GetStringProperty(handle, HidNativeMethods.HidD_GetProductString);
                var manufacturerName = HidNativeMethods.GetStringProperty(handle, HidNativeMethods.HidD_GetManufacturerString);

                // 7. Serial number (if available)
                var serialNumber = HidNativeMethods.GetStringProperty(handle, HidNativeMethods.HidD_GetSerialNumberString);

                // 8. Build HidDeviceInfo (including new fields)
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