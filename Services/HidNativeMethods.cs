using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CustomHidChecker.Services
{
    internal static class HidNativeMethods
    {
        internal const int FILE_FLAG_OVERLAPPED = 0x40000000;
        internal const int FILE_SHARE_READ = 1;
        internal const int FILE_SHARE_WRITE = 2;
        internal const int OPEN_EXISTING = 3;
        internal const uint GENERIC_READ = 0x80000000;
        internal const uint GENERIC_WRITE = 0x40000000;
        internal const int INVALID_HANDLE_VALUE = -1;

        internal static readonly Guid HidClassGuid;

        static HidNativeMethods()
        {
            HidD_GetHidGuid(out HidClassGuid);
        }

        internal static SafeFileHandle CreateFile(string devicePath, uint desiredAccess)
        {
            var handle = CreateFile(devicePath, desiredAccess, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return handle;
        }

        [DllImport("hid.dll")]
        internal static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HidAttributes attributes);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_GetHidDescriptor(SafeFileHandle hidDeviceObject, ref HidDescriptor descriptor, int descriptorLength);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_GetReportDescriptor(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_GetProductString(SafeFileHandle hidDeviceObject, byte[] buffer, int bufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_GetManufacturerString(SafeFileHandle hidDeviceObject, byte[] buffer, int bufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_SetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_GetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern int HidP_GetCaps(IntPtr preparsedData, out HidP_Caps capabilities);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            int dwShareMode,
            IntPtr lpSecurityAttributes,
            int dwCreationDisposition,
            int dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            string? enumerator,
            IntPtr hwndParent,
            SetupDiGetClassDevsFlags flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            ref Guid interfaceClassGuid,
            int memberIndex,
            ref SpDeviceInterfaceData deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SpDeviceInterfaceData deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            int deviceInterfaceDetailDataSize,
            out int requiredSize,
            IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SpDeviceInterfaceData deviceInterfaceData,
            ref SpDeviceInterfaceDetailData deviceInterfaceDetailData,
            int deviceInterfaceDetailDataSize,
            out int requiredSize,
            IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [StructLayout(LayoutKind.Sequential)]
        internal struct HidAttributes
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct HidClassDescriptor
        {
            public byte DescriptorType;
            public ushort DescriptorLength;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct HidDescriptor
        {
            public byte Length;
            public byte DescriptorType;
            public ushort HidSpecification;
            public byte CountryCode;
            public byte DescriptorCount;
            public HidClassDescriptor Descriptor0;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HIDP_CAPS_INTERNAL
        {
            public short Usage;
            public short UsagePage;
            public short InputReportByteLength;
            public short OutputReportByteLength;
            public short FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public short[] Reserved;
            public short NumberLinkCollectionNodes;
            public short NumberInputButtonCaps;
            public short NumberInputValueCaps;
            public short NumberInputDataIndices;
            public short NumberOutputButtonCaps;
            public short NumberOutputValueCaps;
            public short NumberOutputDataIndices;
            public short NumberFeatureButtonCaps;
            public short NumberFeatureValueCaps;
            public short NumberFeatureDataIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HidP_Caps
        {
            public short Usage;
            public short UsagePage;
            public short InputReportByteLength;
            public short OutputReportByteLength;
            public short FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public short[] Reserved;
            public short NumberLinkCollectionNodes;
            public short NumberInputButtonCaps;
            public short NumberInputValueCaps;
            public short NumberInputDataIndices;
            public short NumberOutputButtonCaps;
            public short NumberOutputValueCaps;
            public short NumberOutputDataIndices;
            public short NumberFeatureButtonCaps;
            public short NumberFeatureValueCaps;
            public short NumberFeatureDataIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SpDeviceInterfaceData
        {
            public int Size;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SpDeviceInterfaceDetailData
        {
            public int Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string DevicePath;
        }

        [Flags]
        internal enum SetupDiGetClassDevsFlags : uint
        {
            Default = 0x00000001,
            Present = 0x00000002,
            AllClasses = 0x00000004,
            Profile = 0x00000008,
            DeviceInterface = 0x00000010,
        }

        internal static string GetStringProperty(SafeFileHandle handle, Func<SafeFileHandle, byte[], int, bool> getter)
        {
            var buffer = new byte[512];
            return getter(handle, buffer, buffer.Length) ? Marshal.PtrToStringUni(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0))?.TrimEnd('\0') ?? string.Empty : string.Empty;
        }

        internal static bool TryGetCapabilities(SafeFileHandle handle, out HidP_Caps caps)
        {
            caps = default;
            if (!HidD_GetPreparsedData(handle, out var preparsed))
            {
                return false;
            }

            try
            {
                if (HidP_GetCaps(preparsed, out caps) != 0)
                {
                    return true;
                }
            }
            finally
            {
                HidD_FreePreparsedData(preparsed);
            }

            return false;
        }

        internal static HidDescriptor GetHidDescriptor(SafeFileHandle handle)
        {
            var descriptor = new HidDescriptor
            {
                Length = (byte)Marshal.SizeOf<HidDescriptor>()
            };

            if (!HidD_GetHidDescriptor(handle, ref descriptor, Marshal.SizeOf<HidDescriptor>()))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return descriptor;
        }

        internal static byte[] GetReportDescriptor(SafeFileHandle handle, int expectedLength)
        {
            var length = expectedLength > 0 ? expectedLength : 1024;
            var buffer = new byte[length];
            if (HidD_GetReportDescriptor(handle, buffer, buffer.Length))
            {
                return buffer;
            }

            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        internal static string GetLastErrorMessage()
        {
            return new Win32Exception(Marshal.GetLastWin32Error()).Message;
        }

        internal static SafeFileHandle CreateFileForReadWrite(string devicePath)
        {
            return CreateFile(devicePath, GENERIC_READ | GENERIC_WRITE);
        }
    }
}
