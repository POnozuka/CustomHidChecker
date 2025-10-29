using CustomHidChecker.Models;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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

                var reportIdDetails = GetReportIdDetails(handle, caps);
                if (reportIdDetails is { TotalUniqueCount: > 0 })
                {
                    topLevelCollectionTotal = Math.Max(topLevelCollectionTotal, reportIdDetails.TotalUniqueCount);
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
                    topLevelCollectionTotal,
                    reportIdDetails?.InputReportIds ?? Array.Empty<byte>(),
                    reportIdDetails?.OutputReportIds ?? Array.Empty<byte>(),
                    reportIdDetails?.FeatureReportIds ?? Array.Empty<byte>()
                );
            }
            catch
            {
                return null;
            }
        }

        private static ReportIdDetails? GetReportIdDetails(SafeFileHandle handle, in HidNativeMethods.HidP_Caps caps)
        {
            if (!HidNativeMethods.HidD_GetPreparsedData(handle, out var preparsedData))
            {
                return null;
            }

            try
            {
                var inputIds = new HashSet<byte>();
                var outputIds = new HashSet<byte>();
                var featureIds = new HashSet<byte>();

                CollectReportIds(inputIds, preparsedData, HidNativeMethods.HidP_ReportType.Input, caps.NumberInputButtonCaps, caps.NumberInputValueCaps);
                CollectReportIds(outputIds, preparsedData, HidNativeMethods.HidP_ReportType.Output, caps.NumberOutputButtonCaps, caps.NumberOutputValueCaps);
                CollectReportIds(featureIds, preparsedData, HidNativeMethods.HidP_ReportType.Feature, caps.NumberFeatureButtonCaps, caps.NumberFeatureValueCaps);

                return new ReportIdDetails(inputIds, outputIds, featureIds);
            }
            finally
            {
                HidNativeMethods.HidD_FreePreparsedData(preparsedData);
            }
        }

        private static void CollectReportIds(HashSet<byte> reportIds, IntPtr preparsedData, HidNativeMethods.HidP_ReportType reportType, short buttonCapsCount, short valueCapsCount)
        {
            if (buttonCapsCount > 0)
            {
                var length = (ushort)buttonCapsCount;
                var buttonCaps = new HidNativeMethods.HidP_ButtonCaps[length];
                var status = HidNativeMethods.HidP_GetButtonCaps(reportType, buttonCaps, ref length, preparsedData);
                if (status >= 0)
                {
                    for (var i = 0; i < length; i++)
                    {
                        reportIds.Add(buttonCaps[i].ReportID);
                    }
                }
            }

            if (valueCapsCount > 0)
            {
                var length = (ushort)valueCapsCount;
                var valueCaps = new HidNativeMethods.HidP_ValueCaps[length];
                var status = HidNativeMethods.HidP_GetValueCaps(reportType, valueCaps, ref length, preparsedData);
                if (status >= 0)
                {
                    for (var i = 0; i < length; i++)
                    {
                        reportIds.Add(valueCaps[i].ReportID);
                    }
                }
            }
        }

        private sealed class ReportIdDetails
        {
            internal ReportIdDetails(HashSet<byte> input, HashSet<byte> output, HashSet<byte> feature)
            {
                InputReportIds = input.OrderBy(id => id).ToArray();
                OutputReportIds = output.OrderBy(id => id).ToArray();
                FeatureReportIds = feature.OrderBy(id => id).ToArray();

                var combined = new HashSet<byte>(InputReportIds);
                combined.UnionWith(OutputReportIds);
                combined.UnionWith(FeatureReportIds);
                TotalUniqueCount = combined.Count;
            }

            internal IReadOnlyList<byte> InputReportIds { get; }

            internal IReadOnlyList<byte> OutputReportIds { get; }

            internal IReadOnlyList<byte> FeatureReportIds { get; }

            internal int TotalUniqueCount { get; }
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