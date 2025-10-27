using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using CustomHidChecker.Models;
using Microsoft.Win32.SafeHandles;

namespace CustomHidChecker.Services
{
    public static class HidEnumerator
    {
        private const int ErrorNoMoreItems = 259;

        public static IReadOnlyList<HidDeviceInfo> Enumerate()
        {
            var devices = new List<HidDeviceInfo>();
            var hidGuid = HidNativeMethods.HidClassGuid;
            var deviceInfoSet = HidNativeMethods.SetupDiGetClassDevs(
                ref hidGuid,
                null,
                IntPtr.Zero,
                HidNativeMethods.SetupDiGetClassDevsFlags.Present | HidNativeMethods.SetupDiGetClassDevsFlags.DeviceInterface);

            if (deviceInfoSet == IntPtr.Zero || deviceInfoSet.ToInt64() == -1)
            {
                return devices;
            }

            try
            {
                for (var index = 0; ; index++)
                {
                    var deviceInterfaceData = new HidNativeMethods.SpDeviceInterfaceData
                    {
                        Size = Marshal.SizeOf<HidNativeMethods.SpDeviceInterfaceData>()
                    };

                    if (!HidNativeMethods.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref deviceInterfaceData))
                    {
                        var error = Marshal.GetLastWin32Error();
                        if (error == ErrorNoMoreItems)
                        {
                            break;
                        }

                        throw new Win32Exception(error);
                    }

                    var requiredSize = 0;
                    HidNativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);

                    var detailData = new HidNativeMethods.SpDeviceInterfaceDetailData
                    {
                        Size = IntPtr.Size == 8 ? 8 : 4 + Marshal.SystemDefaultCharSize
                    };

                    if (!HidNativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, ref detailData, Marshal.SizeOf(detailData), out _, IntPtr.Zero))
                    {
                        continue;
                    }

                    var devicePath = detailData.DevicePath;
                    try
                    {
                        using var handle = HidNativeMethods.CreateFileForReadWrite(devicePath);
                        if (handle.IsInvalid)
                        {
                            continue;
                        }

                        var attributes = new HidNativeMethods.HidAttributes
                        {
                            Size = Marshal.SizeOf<HidNativeMethods.HidAttributes>()
                        };

                        if (!HidNativeMethods.HidD_GetAttributes(handle, ref attributes))
                        {
                            continue;
                        }

                        if (!HidNativeMethods.TryGetCapabilities(handle, out var caps))
                        {
                            continue;
                        }

                        var productName = HidNativeMethods.GetStringProperty(handle, HidNativeMethods.HidD_GetProductString);
                        var manufacturerName = HidNativeMethods.GetStringProperty(handle, HidNativeMethods.HidD_GetManufacturerString);

                        devices.Add(new HidDeviceInfo(
                            devicePath,
                            attributes.VendorID,
                            attributes.ProductID,
                            attributes.VersionNumber,
                            caps.InputReportByteLength,
                            caps.OutputReportByteLength,
                            caps.FeatureReportByteLength,
                            string.IsNullOrWhiteSpace(productName) ? null : productName,
                            string.IsNullOrWhiteSpace(manufacturerName) ? null : manufacturerName));
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                HidNativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return devices;
        }
    }
}
