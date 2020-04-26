using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace AnyLayout.RawInput
{
    [SuppressUnmanagedCodeSecurity]
    public static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputDevice
        {
            public IntPtr Handle;
            public RawInputDeviceType Type;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputHeader
        {
            public RawInputDeviceType Type;
            public uint Size;
            public IntPtr DeviceHandle;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct RawMouse
        {
            ///<summary>Indicator flags.</summary>
            [FieldOffset(0)]
            public ushort usFlags;

            ///<summary>The transition state of the mouse buttons.</summary>
            [FieldOffset(4)]
            public ushort ButtonFlags;

            [FieldOffset(6)]
            public ushort ButtonData;

            ///<summary>The raw state of the mouse buttons.</summary>
            [FieldOffset(8)]
            public uint RawButtons;

            ///<summary>The signed relative or absolute motion in the X direction.</summary>
            [FieldOffset(12)]
            public int LastX;

            ///<summary>The signed relative or absolute motion in the Y direction.</summary>
            [FieldOffset(16)]
            public int LastY;

            ///<summary>Device-specific additional information for the event.</summary>
            [FieldOffset(20)]
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RawKeyboard
        {
            public ushort MakeCode;
            public RawInputKeyboardFlags Flags;
            public ushort Reserved;
            public ushort VirtualKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [Flags]
        public enum RawInputKeyboardFlags : ushort
        {
            Make = 0,
            Break = 1,
            E0 = 2,
            E1 = 4,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RawInput
        {
            public RawInputHeader Header;
            public RawInputAny Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RawHidHeader
        {
            public uint SizeHid;
            public uint Count;
            public byte Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct RawInputAny
        {
            [FieldOffset(0)]
            public RawMouse Mouse;
            [FieldOffset(0)]
            public RawKeyboard Keyboard;
            [FieldOffset(0)]
            public RawHidHeader Hid;
        }

        public enum RawInputDataCommand : uint
        {
            Header = 0x10000005,
            Input = 0x10000003,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputDeviceInfoHid
        {
            public uint VendorId;
            public uint ProductId;
            public uint VersionNumber;
            public HidUsagePage UsagePage;
            public ushort Usage;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputDeviceInfoKeyboard
        {
            public uint Type;
            public uint SubType;
            public uint KeyboardMode;
            public uint NumberOfFunctionKeys;
            public uint NumberOfIndicators;
            public uint NumberOfKeysTotal;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputDeviceInfoMouse
        {
            public uint Id;
            public uint NumberOfButtons;
            public uint SampleRate;
            public uint HasHorizontalWheel;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct RawInputDeviceInfo
        {
            [FieldOffset(0)]
            public uint Size;
            [FieldOffset(4)]
            public RawInputDeviceType Type;
            [FieldOffset(8)]
            public RawInputDeviceInfoMouse Mouse;
            [FieldOffset(8)]
            public RawInputDeviceInfoKeyboard Keyboard;
            [FieldOffset(8)]
            public RawInputDeviceInfoHid Hid;
        }

        public enum RawInputDeviceInfoCommand : uint
        {
            DeviceName = 0x20000007,
            DeviceInfo = 0x2000000b,
            PreparsedData = 0x20000005,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputDeviceRegisgtration
        {
            public HidUsagePage UsagePage;
            public ushort Usage;
            public RawInputDeviceFlags Flags;
            public IntPtr TargetHandle;
        }

        public enum RawInputDeviceFlags
        {
            /// <summary>If set, the application command keys are handled. <see cref="AppKeys"/> can be specified only if <see cref="NoLegacy"/> is specified for a keyboard device.</summary>
            AppKeys = 0x00000400,
            /// <summary>If set, the mouse button click does not activate the other window.</summary>
            CaptureMouse = 0x00000200,
            /// <summary>If set, this enables the caller to receive WM_INPUT_DEVICE_CHANGE notifications for device arrival and device removal.</summary>
            /// <remarks>Windows XP:  This flag is not supported until Windows Vista</remarks>
            DevNotify = 0x00002000,

            /// <summary>If set, this specifies the top level collections to exclude when reading a complete usage page. This flag only affects a TLC whose usage page is already specified with <see cref="PageOnly"/>.</summary>
            Exclude = 0x00000010,
            /// <summary>If set, this enables the caller to receive input in the background only if the foreground application does not process it. In other words, if the foreground application is not registered for raw input, then the background application that is registered will receive the input.</summary>
            /// <remarks>Windows XP:  This flag is not supported until Windows Vista</remarks>
            ExInputSink = 0x00001000,

            /// <summary>If set, this enables the caller to receive the input even when the caller is not in the foreground. Note that hwndTarget must be specified.</summary>
            InputSink = 0x00000100,
            /// <summary>If set, the application-defined keyboard device hotkeys are not handled. However, the system hotkeys; for example, ALT+TAB and CTRL+ALT+DEL, are still handled. By default, all keyboard hotkeys are handled. RIDEV_NOHOTKEYS can be specified even if RIDEV_NOLEGACY is not specified and hwndTarget is NULL.</summary>
            NoHotKeys = 0x00000200,
            /// <summary>If set, this prevents any devices specified by UsagePage or Usage from generating legacy messages. This is only for the mouse and keyboard.</summary>
            NoLegacy = 0x00000030,
            /// <summary>If set, this specifies all devices whose top level collection is from the specified UsagePage. Note that Usage must be zero. To exclude a particular top level collection, use <see cref="Exclude"/>.</summary>
            PageOnly = 0x00000020,
            /// <summary>If set, this removes the top level collection from the inclusion list. This tells the operating system to stop reading from a device which matches the top level collection.</summary>
            Remove = 0x00000001,
        }

        public struct HidParsingCaps
        {
            public ushort Usage;
            public HidUsagePage UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            // There are currently 17 reserved / undocumented values in the middle of the structure.
#pragma warning disable CS0169, IDE0051, RCS1213
            private readonly ushort _reserved0;
            private readonly ushort _reserved1;
            private readonly ushort _reserved2;
            private readonly ushort _reserved3;
            private readonly ushort _reserved4;
            private readonly ushort _reserved5;
            private readonly ushort _reserved6;
            private readonly ushort _reserved7;
            private readonly ushort _reserved8;
            private readonly ushort _reserved9;
            private readonly ushort _reserved10;
            private readonly ushort _reserved11;
            private readonly ushort _reserved12;
            private readonly ushort _reserved13;
            private readonly ushort _reserved14;
            private readonly ushort _reserved15;
            private readonly ushort _reserved16;
#pragma warning restore CS0169, IDE0051, RCS1213
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [DllImport("user32", EntryPoint = "GetRawInputDeviceList", SetLastError = true)]
        public static extern uint GetRawInputDeviceList(IntPtr zero, ref uint deviceCount, uint deviceSize);

        [DllImport("user32", EntryPoint = "GetRawInputDeviceList", SetLastError = true)]
        public static extern uint GetRawInputDeviceList(ref RawInputDevice rawInputDeviceList, ref uint deviceCount, uint deviceSize);

        [DllImport("user32", EntryPoint = "GetRawInputDeviceInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetRawInputDeviceInfo(IntPtr hDevice, RawInputDeviceInfoCommand uiCommand, out RawInputDeviceInfo deviceInfo, ref uint pcbSize);

        [DllImport("user32", EntryPoint = "GetRawInputDeviceInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetRawInputDeviceInfo(IntPtr hDevice, RawInputDeviceInfoCommand uiCommand, ref char firstLetter, ref uint pcbSize);

        [DllImport("user32", EntryPoint = "RegisterRawInputDevices", SetLastError = true)]
        public static extern int RegisterRawInputDevices(RawInputDeviceRegisgtration[] rawInputDevices, uint deviceCount, uint deviceSize);

        [DllImport("user32", EntryPoint = "GetRawInputData", SetLastError = true)]
        public static extern uint GetRawInputData(IntPtr rawInputHandle, RawInputDataCommand command, IntPtr zero, ref uint dataSizeInBytes, uint headerSizeInBytes);

        [DllImport("user32", EntryPoint = "GetRawInputData", SetLastError = true)]
        public static extern uint GetRawInputData(IntPtr rawInputHandle, RawInputDataCommand command, ref byte firstByte, ref uint dataSizeInBytes, uint headerSizeInBytes);

        [DllImport("hid", EntryPoint = "HidD_GetPreparsedData", SetLastError = true)]
        public static extern int HidDiscoveryGetPreparsedData(SafeFileHandle deviceFileHandle, out IntPtr preparsedData);

        [DllImport("hid", EntryPoint = "HidD_FreePreparsedData", SetLastError = true)]
        public static extern int HidDiscoveryFreePreparsedData(IntPtr preparsedData);

        [DllImport("hid", EntryPoint = "HidD_GetManufacturerString", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int HidDiscoveryGetManufacturerString(SafeFileHandle deviceFileHandle, ref char buffer, uint bufferLength);

        [DllImport("hid", EntryPoint = "HidD_GetProductString", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int HidDiscoveryGetProductString(SafeFileHandle deviceFileHandle, ref char buffer, uint bufferLength);

        [DllImport("hid", EntryPoint = "HidP_GetCaps", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int HidParsingGetCaps(IntPtr preparsedData, out HidParsingCaps capabilities);

        [DllImport("kernel32", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(string fileName, FileAccess desiredAccess, FileShare shareMode, IntPtr securityAttributes, FileMode creationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        public delegate int HidGetStringFunction(SafeFileHandle deviceFileHandle, ref char buffer, uint bufferLength);

        private static readonly HidGetStringFunction HidGetManufacturerStringFunction = HidDiscoveryGetManufacturerString;
        private static readonly HidGetStringFunction HidGetProductStringFunction = HidDiscoveryGetProductString;

        public static RawInputDevice[] GetDevices()
        {
            uint count = 0;
            if (GetRawInputDeviceList(IntPtr.Zero, ref count, (uint)Unsafe.SizeOf<RawInputDevice>()) == uint.MaxValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            if (count == 0) return Array.Empty<RawInputDevice>();
            var devices = new RawInputDevice[count];
            if ((count = GetRawInputDeviceList(ref devices[0], ref count, (uint)Unsafe.SizeOf<RawInputDevice>())) == uint.MaxValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (count != (uint)devices.Length)
            {
                if (count < (uint)devices.Length) Array.Resize(ref devices, (int)count);
                else throw new Exception("Unexpected number of devices returned by GetRawInputDeviceList.");
            }

            return devices;
        }

        public static string GetManufacturerString(SafeFileHandle deviceHandle)
            => GetHidString(deviceHandle, HidGetManufacturerStringFunction, "HidD_GetManufacturerString");

        public static string GetProductString(SafeFileHandle deviceHandle)
            => GetHidString(deviceHandle, HidGetProductStringFunction, "HidD_GetProductString");

        private static string GetHidString(SafeFileHandle deviceHandle, HidGetStringFunction getHidString, string functionName)
        {
            int bufferLength = 256;
            while (true)
            {
                var buffer = ArrayPool<char>.Shared.Rent(bufferLength);
                try
                {
                    if (getHidString(deviceHandle, ref buffer[0], (uint)buffer.Length * 2) == 0)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    return Array.IndexOf(buffer, '\0') is int endIndex && endIndex >= 0 ?
                        buffer.AsSpan(0, endIndex).ToString() :
                        throw new Exception($"The string received from {functionName} was not null-terminated.");
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer);
                }
            }
        }

        public static string GetDeviceName(IntPtr deviceHandle)
        {
            int bufferLength = 256;
            while (true)
            {
                var buffer = ArrayPool<char>.Shared.Rent(bufferLength);
                try
                {
                    bufferLength = buffer.Length;
                    uint count = GetRawInputDeviceInfo(deviceHandle, RawInputDeviceInfoCommand.DeviceName, ref buffer[0], ref Unsafe.As<int, uint>(ref bufferLength));

                    if (count != uint.MaxValue) return buffer.AsSpan(0, (int)count).ToString();
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer);
                }
            }
        }

        public static RawInput GetRawInputData(IntPtr rawInputHandle)
        {
            uint dataSize = 0;
            if (GetRawInputData(rawInputHandle, RawInputDataCommand.Input, IntPtr.Zero, ref dataSize, (uint)Unsafe.SizeOf<RawInputHeader>()) == uint.MaxValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            var data = ArrayPool<byte>.Shared.Rent(checked((int)dataSize));
            try
            {
                if (GetRawInputData(rawInputHandle, RawInputDataCommand.Input, ref data[0], ref dataSize, (uint)Unsafe.SizeOf<RawInputHeader>()) is uint count && count != uint.MaxValue)
                {
                    Span<RawInput> result = stackalloc RawInput[1];
                    data.AsSpan(0, Math.Min((int)count, Unsafe.SizeOf<RawInput>())).CopyTo(MemoryMarshal.Cast<RawInput, byte>(result));
                    return result[0];
                }
                else
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(data);
            }
        }

        public static RawInputDeviceInfo GetDeviceInfo(IntPtr deviceHandle)
        {
            var result = default(RawInputDeviceInfo);
            uint bufferLength = (uint)Unsafe.SizeOf<RawInputDeviceInfo>();
            return GetRawInputDeviceInfo(deviceHandle, RawInputDeviceInfoCommand.DeviceInfo, out result, ref bufferLength) switch
            {
                uint.MaxValue => throw new Win32Exception(Marshal.GetLastWin32Error()),
                _ => result
            };
        }
    }
}
