using CustomHidChecker.Models;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
                    $"HID Device: Path={device.DevicePath}, VID=0x{device.VendorId:X4}, PID=0x{device.ProductId:X4}, Version=0x{device.VersionNumber:X4}, Input={device.InputReportLength}, Output={device.OutputReportLength}, Feature={device.FeatureReportLength}, TLCIndex={device.TopLevelCollectionIndex}/{device.TopLevelCollectionTotal}, TLCNodes={device.TopLevelCollectionCount}, ProductName={device.ProductName ?? "(null)"}, ManufacturerName={device.ManufacturerName ?? "(null)"}, InstanceId={device.InstanceId ?? "(null)"}, SerialNumber={device.SerialNumber ?? "(null)"}");
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

                var topLevelCollectionIndex = GetTopLevelCollectionIndex(devicePath);
                if (topLevelCollectionIndex <= 0)
                {
                    topLevelCollectionIndex = 1;
                }

                var topLevelCollectionTotal = Math.Max(1, (int)caps.NumberLinkCollectionNodes);

                try
                {
                    var reportDescriptor = TryGetReportDescriptor(handle);
                    if (reportDescriptor is { Length: > 0 })
                    {
                        var counted = CountTopLevelCollections(reportDescriptor);
                        if (counted > 0)
                        {
                            topLevelCollectionTotal = counted;
                        }
                    }
                }
                catch
                {
                    topLevelCollectionTotal = Math.Max(topLevelCollectionTotal, 1);
                }

                if (topLevelCollectionTotal < topLevelCollectionIndex)
                {
                    topLevelCollectionTotal = topLevelCollectionIndex;
                }

                // 8. Build HidDeviceInfo (including new fields)
                return new HidDeviceInfo(
                    devicePath,
                    attributes.VendorID,
                    attributes.ProductID,
                    attributes.VersionNumber,
                    caps.InputReportByteLength,
                    caps.OutputReportByteLength,
                    caps.FeatureReportByteLength,
                    caps.NumberLinkCollectionNodes,
                    string.IsNullOrWhiteSpace(productName) ? null : productName,
                    string.IsNullOrWhiteSpace(manufacturerName) ? null : manufacturerName,
                    string.IsNullOrWhiteSpace(instanceId) ? null : instanceId,
                    string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber,
                    topLevelCollectionIndex,
                    topLevelCollectionTotal
                );
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? TryGetReportDescriptor(SafeFileHandle handle)
        {
            var buffer = new byte[4096];
            if (!HidNativeMethods.HidD_GetReportDescriptor(handle, buffer, buffer.Length))
            {
                return null;
            }

            var length = buffer.Length;
            while (length > 0 && buffer[length - 1] == 0)
            {
                length--;
            }

            if (length <= 0)
            {
                return null;
            }

            if (length == buffer.Length)
            {
                return buffer;
            }

            var descriptor = new byte[length];
            Array.Copy(buffer, descriptor, length);
            return descriptor;
        }

        private static int CountTopLevelCollections(byte[] descriptor)
        {
            var depth = 0;
            var topLevelCount = 0;
            var index = 0;

            while (index < descriptor.Length)
            {
                var prefix = descriptor[index++];

                if (prefix == 0xFE)
                {
                    if (index + 1 >= descriptor.Length)
                    {
                        break;
                    }

                    var size = descriptor[index];
                    index += 2;
                    if (index + size > descriptor.Length)
                    {
                        break;
                    }

                    index += size;
                    continue;
                }

                var sizeCode = prefix & 0x03;
                var type = (prefix >> 2) & 0x03;
                var tag = (prefix >> 4) & 0x0F;

                var dataLength = sizeCode switch
                {
                    0 => 0,
                    1 => 1,
                    2 => 2,
                    3 => 4,
                    _ => 0
                };

                if (index + dataLength > descriptor.Length)
                {
                    break;
                }

                if (type == 0 && tag == 0x0A)
                {
                    if (depth == 0)
                    {
                        topLevelCount++;
                    }

                    depth++;
                }
                else if (type == 0 && tag == 0x0C)
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }

                index += dataLength;
            }

            return topLevelCount > 0 ? topLevelCount : 1;
        }

        private static int GetTopLevelCollectionIndex(string devicePath)
        {
            if (string.IsNullOrWhiteSpace(devicePath))
            {
                return 1;
            }

            var colMarkerIndex = devicePath.IndexOf("&col", StringComparison.OrdinalIgnoreCase);
            if (colMarkerIndex < 0)
            {
                return 1;
            }

            var start = colMarkerIndex + 4;
            var end = start;
            while (end < devicePath.Length && char.IsLetterOrDigit(devicePath[end]))
            {
                end++;
            }

            if (end <= start)
            {
                return 1;
            }

            var slice = devicePath.Substring(start, end - start);
            if (int.TryParse(slice, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                return value > 0 ? value : 1;
            }

            if (int.TryParse(slice, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value > 0 ? value : 1;
            }

            return 1;
        }

    }
}