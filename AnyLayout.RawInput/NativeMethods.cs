using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace AnyLayout.RawInput
{
    [SuppressUnmanagedCodeSecurity]
    public static class NativeMethods
    {
        private const int ErrorGenFailure = 0x0000001F;
        private const int ErrorInvalidUserBuffer = 0x000006F8;
        private const int ErrorNoAccess = 0x000003E6;

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
            TerminalServerSetLed = 8,
            TerminalServerShadow = 0x10,
            TerminalServerVkPacket = 0x20,
            RawInputMessageVkey = 0x40,
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

        [StructLayout(LayoutKind.Sequential)]
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
            public ushort LinkCollectionNodesCount;
            public ushort InputButtonCapsCount;
            public ushort InputValueCapsCount;
            public ushort InputDataIndicesCount;
            public ushort OutputButtonCapsCount;
            public ushort OutputValueCapsCount;
            public ushort OutputDataIndicesCount;
            public ushort FeatureButtonCapsCount;
            public ushort FeatureValueCapsCount;
            public ushort FeatureDataIndicesCount;
        }

        // NB: Win32 boolean are mapped to C# bool here. Provided the only two possible values of TRUE (1) and FALSE (0) are used, this should work fine.
        [StructLayout(LayoutKind.Explicit)]
        public struct HidParsingButtonCaps
        {
            [FieldOffset(0)]
            public HidUsagePage UsagePage;
            [FieldOffset(2)]
            public byte ReportID;
            [FieldOffset(3)]
            public bool IsAlias;
            [FieldOffset(4)]
            public ushort BitField;
            [FieldOffset(6)]
            public ushort LinkCollection;
            [FieldOffset(8)]
            public ushort LinkUsage;
            [FieldOffset(10)]
            public HidUsagePage LinkUsagePage;
            [FieldOffset(12)]
            public bool IsRange;
            [FieldOffset(13)]
            public bool IsStringRange;
            [FieldOffset(14)]
            public bool IsDesignatorRange;
            [FieldOffset(15)]
            public bool IsAbsolute;

            // There are currently 10 reserved / undocumented uint values in the middle of the structure.

            [FieldOffset(56)]
            public RangeCaps Range;
            [FieldOffset(56)]
            public NotRangeCaps NotRange;
        }

        // NB: Win32 boolean are mapped to C# bool here. Provided the only two possible values of TRUE (1) and FALSE (0) are used, this should work fine.
        [StructLayout(LayoutKind.Explicit)]
        public struct HidParsingValueCaps
        {
            [FieldOffset(0)]
            public HidUsagePage UsagePage;
            [FieldOffset(2)]
            public byte ReportID;
            [FieldOffset(3)]
            public bool IsAlias;
            [FieldOffset(4)]
            public ushort BitField;
            [FieldOffset(6)]
            public ushort LinkCollection;
            [FieldOffset(8)]
            public ushort LinkUsage;
            [FieldOffset(10)]
            public HidUsagePage LinkUsagePage;
            [FieldOffset(12)]
            public bool IsRange;
            [FieldOffset(13)]
            public bool IsStringRange;
            [FieldOffset(14)]
            public bool IsDesignatorRange;
            [FieldOffset(15)]
            public bool IsAbsolute;
            [FieldOffset(16)]
            public bool HasNull;

            // There is one reserved byte for padding

            [FieldOffset(18)]
            public ushort BitSize;
            [FieldOffset(20)]
            public ushort ReportCount;

            // There are currently 5 reserved / undocumented ushort values in the middle of the structure.

            [FieldOffset(32)]
            public uint UnitsExp;
            [FieldOffset(36)]
            public uint Units;
            [FieldOffset(40)]
            public int LogicalMin;
            [FieldOffset(44)]
            public int LogicalMax;
            [FieldOffset(48)]
            public int PhysicalMin;
            [FieldOffset(52)]
            public int PhysicalMax;

            [FieldOffset(56)]
            public RangeCaps Range;
            [FieldOffset(56)]
            public NotRangeCaps NotRange;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RangeCaps
        {
            public ushort UsageMin;
            public ushort UsageMax;
            public ushort StringMin;
            public ushort StringMax;
            public ushort DesignatorMin;
            public ushort DesignatorMax;
            public ushort DataIndexMin;
            public ushort DataIndexMax;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NotRangeCaps
        {
            // A few fields are left unused compared to the alternative Range structure.
#pragma warning disable CS0169, IDE0051, RCS1213
            private readonly ushort _reserved1;
            public ushort Usage;
            public ushort StringIndex;
            private readonly ushort _reserved2;
            public ushort DesignatorIndex;
            private readonly ushort _reserved3;
            public ushort DataIndex;
            private readonly ushort _reserved4;
#pragma warning restore CS0169, IDE0051, RCS1213
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HidParsingLinkCollectionNode
        {
            private static readonly byte IsAliasMask = BitConverter.IsLittleEndian ? (byte)0x01 : (byte)0x80;

            public ushort LinkUsage;
            public HidUsagePage LinkUsagePage;
            public ushort Parent;
            public ushort ChildCount;
            public ushort NextSibling;
            public ushort FirstChild;

            // The following fields are declared as C++ bitfields, which means that the layout slightly differs between little & big endian… great ! 😣
            // NB: The bitfield should be aligned as an uint. (The sequential layout is already sufficently packed in that regard)
            public HidCollectionType CollectionType; // The first byte-sized field should be mostly unaffected.
            private byte _isAlias; // However, this following one bit-sized field would be 0x80 on big endian, and 0x01 on little endian.
            private readonly ushort _reserved; // Thankfully, nothing else is used for now…

            public bool IsAlias
            {
                get => (_isAlias & IsAliasMask) != 0;
                set => _isAlias = value ? (byte)(_isAlias | IsAliasMask) : (byte)(_isAlias & ~IsAliasMask);
            }

            public IntPtr UserContext;
        }

        public enum HidParsingReportType
        {
            Input = 0,
            Output = 1,
            Feature = 2,
        }

        public enum HidParsingResult : uint
        {
            Success = 0x00110000,
            Null = 0x80110001,
            InvalidPreparsedData = 0xC0110001,
            InvalidReportType = 0xC0110002,
            InvalidReportLength = 0xC0110003,
            UsageNotFound = 0xC0110004,
            ValueOutOfRange = 0xC0110005,
            BadLogPhyValues = 0xC0110006,
            BufferTooSmall = 0xC0110007,
            InternalError = 0xC0110008,
            I8042_TRANS_UNKNOWN = 0xC0110009,
            IncompatibleReportId = 0xC011000A,
            NotValueArray = 0xC011000B,
            IsValueArray = 0xC011000C,
            DataIndexNotFound = 0xC011000D,
            DataIndexOutOfRange = 0xC011000E,
            ButtonNotPressed = 0xC011000F,
            ReportDoesNotExist = 0xC0110010,
            NotImplemented = 0xC0110020,
        }

        [DllImport("user32", EntryPoint = "GetRawInputDeviceList", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetRawInputDeviceList(IntPtr zero, ref uint deviceCount, uint deviceSize);

        [DllImport("user32", EntryPoint = "GetRawInputDeviceList", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetRawInputDeviceList(ref RawInputDevice rawInputDeviceList, ref uint deviceCount, uint deviceSize);

        [DllImport("user32", EntryPoint = "GetRawInputDeviceInfoW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetRawInputDeviceInfo(IntPtr hDevice, RawInputDeviceInfoCommand uiCommand, IntPtr zero, ref uint pcbSize);

        [DllImport("user32", EntryPoint = "GetRawInputDeviceInfoW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetRawInputDeviceInfo(IntPtr hDevice, RawInputDeviceInfoCommand uiCommand, out RawInputDeviceInfo deviceInfo, ref uint pcbSize);

        [DllImport("user32", EntryPoint = "GetRawInputDeviceInfoW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetRawInputDeviceInfo(IntPtr hDevice, RawInputDeviceInfoCommand uiCommand, ref char firstLetter, ref uint pcbSize);

        [DllImport("user32", EntryPoint = "GetRawInputDeviceInfoW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetRawInputDeviceInfo(IntPtr hDevice, RawInputDeviceInfoCommand uiCommand, ref byte firstByte, ref uint pcbSize);

        [DllImport("user32", EntryPoint = "RegisterRawInputDevices", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegisterRawInputDevices(RawInputDeviceRegisgtration[] rawInputDevices, uint deviceCount, uint deviceSize);

        [DllImport("user32", EntryPoint = "GetRawInputData", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetRawInputData(IntPtr rawInputHandle, RawInputDataCommand command, IntPtr zero, ref uint dataSizeInBytes, uint headerSizeInBytes);

        [DllImport("user32", EntryPoint = "GetRawInputData", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetRawInputData(IntPtr rawInputHandle, RawInputDataCommand command, ref byte firstByte, ref uint dataSizeInBytes, uint headerSizeInBytes);

        [DllImport("hid", EntryPoint = "HidD_GetPreparsedData", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int HidDiscoveryGetPreparsedData(SafeFileHandle deviceFileHandle, out IntPtr preparsedData);

        [DllImport("hid", EntryPoint = "HidD_FreePreparsedData", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int HidDiscoveryFreePreparsedData(IntPtr preparsedData);

        [DllImport("hid", EntryPoint = "HidD_GetManufacturerString", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int HidDiscoveryGetManufacturerString(SafeFileHandle deviceFileHandle, ref char buffer, uint bufferLength);

        [DllImport("hid", EntryPoint = "HidD_GetProductString", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int HidDiscoveryGetProductString(SafeFileHandle deviceFileHandle, ref char buffer, uint bufferLength);

        [DllImport("hid", EntryPoint = "HidD_GetIndexedString", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int HidDiscoveryGetIndexedString(SafeFileHandle deviceFileHandle, uint StringIndex, ref char firstChar, uint bufferLength);

        [DllImport("hid", EntryPoint = "HidD_GetPhysicalDescriptor", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int HidDiscoveryGetPhysicalDescriptor(SafeFileHandle deviceFileHandle, ref byte firstByte, uint bufferLength);

        [DllImport("hid", EntryPoint = "HidP_GetCaps", ExactSpelling = true, CharSet = CharSet.Unicode)]
        public static extern HidParsingResult HidParsingGetCaps(ref byte preparsedData, out HidParsingCaps capabilities);

        [DllImport("hid", EntryPoint = "HidP_GetButtonCaps", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern HidParsingResult HidParsingGetButtonCaps(HidParsingReportType reportType, ref /* HidParsingButtonCaps */ byte firstButtonCap, ref ushort buttonCapsLength, ref byte preparsedData);

        // Work around P/Invoke refusing to consider bool as blittable…
        public static HidParsingResult HidParsingGetButtonCaps(HidParsingReportType reportType, ref HidParsingButtonCaps firstButtonCap, ref ushort buttonCapsLength, ref byte preparsedData)
            => HidParsingGetButtonCaps(reportType, ref Unsafe.As<HidParsingButtonCaps, byte>(ref firstButtonCap), ref buttonCapsLength, ref preparsedData);

        [DllImport("hid", EntryPoint = "HidP_GetValueCaps", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern HidParsingResult HidParsingGetValueCaps(HidParsingReportType reportType, ref /* HidParsingValueCaps */ byte firstValueCap, ref ushort valueCapsLength, ref byte preparsedData);

        // Work around P/Invoke refusing to consider bool as blittable…
        public static HidParsingResult HidParsingGetValueCaps(HidParsingReportType reportType, ref HidParsingValueCaps firstValueCap, ref ushort buttonCapsLength, ref byte preparsedData)
            => HidParsingGetValueCaps(reportType, ref Unsafe.As<HidParsingValueCaps, byte>(ref firstValueCap), ref buttonCapsLength, ref preparsedData);

        [DllImport("hid", EntryPoint = "HidP_GetLinkCollectionNodes", ExactSpelling = true, CharSet = CharSet.Unicode)]
        public static extern HidParsingResult HidParsingGetLinkCollectionNodes(ref HidParsingLinkCollectionNode firstNode, ref uint linkCollectionNodesLength, ref byte preparsedData);

        [DllImport("kernel32", EntryPoint = "CreateFileW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
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
                buffer[0] = '\0';
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

        public static byte[] GetPreparsedData(IntPtr deviceHandle)
        {
            int bufferLength = 0;
            uint count;
            byte[] buffer;
            // Usually, this code should succeed at first try. The risk of allocating multiple "big" arrays should be pretty low.
            do
            {
                GetRawInputDeviceInfo(deviceHandle, RawInputDeviceInfoCommand.PreparsedData, default, ref Unsafe.As<int, uint>(ref bufferLength));

                // Don't know when or why this would happen, but
                if (bufferLength == 0) return Array.Empty<byte>();

                buffer = new byte[bufferLength];
                count = GetRawInputDeviceInfo(deviceHandle, RawInputDeviceInfoCommand.PreparsedData, ref buffer[0], ref Unsafe.As<int, uint>(ref bufferLength));
            }
            while (count == uint.MaxValue);

            if ((int)count < buffer.Length)
            {
                // In the event where the buffer would actually not have been used up entirely, despite having queryed for the correct size beforehand…
                Array.Resize(ref buffer, (int)count);
            }

            return buffer;
        }

        public static string GetIndexedString(SafeFileHandle deviceHandle, uint stringIndex)
            => GetIndexedStringOnStack(deviceHandle, stringIndex) ?? GetIndexedStringInPooledArray(deviceHandle, stringIndex);

        private static string? GetIndexedStringOnStack(SafeFileHandle deviceHandle, uint stringIndex)
        {
            // From the docs: USB devices shouldn't return more than 126+1 characters. Other devices could return more.
            Span<char> text = stackalloc char[127];
            if (HidDiscoveryGetIndexedString(deviceHandle, stringIndex, ref text.GetPinnableReference(), (uint)text.Length) == 0)
            {
                switch (Marshal.GetLastWin32Error())
                {
                    case ErrorInvalidUserBuffer:
                        return null;
                    case int code:
                        throw new Win32Exception(code);
                }
            }

            return (text.IndexOf('\0') is int endIndex && endIndex >= 0 ? text.Slice(0, endIndex) : text).ToString();
        }

        private static string GetIndexedStringInPooledArray(SafeFileHandle deviceHandle, uint stringIndex)
        {
            const int MaxLength = 65536; // Arbitrary limit on the size we allow ourselves to request.
            int length = 256;

            // The loop will either exit by returning a valid string, or by throwing an exception.
            while (true)
            {
                var text = ArrayPool<char>.Shared.Rent(length);
                try
                {
                    length = text.Length;

                    if (HidDiscoveryGetIndexedString(deviceHandle, stringIndex, ref text[0], (uint)text.Length) != 0)
                    {
                        return (Array.IndexOf(text, '\0', 0) is int endIndex && endIndex >= 0 ? text.AsSpan(0, endIndex) : text.AsSpan()).ToString();
                    }

                    switch (Marshal.GetLastWin32Error())
                    {
                        case ErrorInvalidUserBuffer when length < MaxLength:
                            length = Math.Min(2 * length, MaxLength);
                            continue;
                        case int code:
                            throw new Win32Exception(code);
                    }
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(text);
                }
            }
        }

        public static PhysicalDescriptorSetCollection GetPhysicalDescriptor(SafeFileHandle deviceHandle)
            => GetPhysicalDescriptorOnStack(deviceHandle) ?? GetPhysicalDescriptorInPooledArray(deviceHandle);

        private static PhysicalDescriptorSetCollection? GetPhysicalDescriptorOnStack(SafeFileHandle deviceHandle)
        {
            Span<byte> data = stackalloc byte[256];
            if (HidDiscoveryGetPhysicalDescriptor(deviceHandle, ref data.GetPinnableReference(), (uint)data.Length) == 0)
            {
                switch (Marshal.GetLastWin32Error())
                {
                    // Assume that ERROR_GEN_FAILURE means that the device has no physical descriptor. (Should be the most common case)
                    // Not sure yet what is the cause of ERROR_NO_ACCESS, but it might be returned for buggy devices.
                    case ErrorGenFailure:
                    case ErrorNoAccess:
                        return PhysicalDescriptorSetCollection.Empty;
                    case ErrorInvalidUserBuffer:
                        return null;
                    case int code:
                        throw new Win32Exception(code);
                }
            }

            return new PhysicalDescriptorSetCollection(data, false);
        }

        private static PhysicalDescriptorSetCollection GetPhysicalDescriptorInPooledArray(SafeFileHandle deviceHandle)
        {
            // The absolute maximum data length of the physical descriptor set collection is around 32 MB (With 255 descriptor sets of 65535 descriptors each)
            const int MaxLength = 3 + 255 * (1 + 2 * 65535);
            // Start with a 4k buffer, which should hopefully be a reasonable size for most physical descriptors.
            int length = 4096;

            // The loop will either exit by returning a valid collection, or by throwing an exception.
            while (true)
            {
                var data = ArrayPool<byte>.Shared.Rent(length);
                try
                {
                    length = data.Length;

                    if (HidDiscoveryGetPhysicalDescriptor(deviceHandle, ref data[0], (uint)data.Length) != 0)
                    {
                        return new PhysicalDescriptorSetCollection(data, false);
                    }

                    switch (Marshal.GetLastWin32Error())
                    {
                        // Assume that ERROR_GEN_FAILURE means that the device has no physical descriptor. (Should be the most common case)
                        // Not sure yet what is the cause of ERROR_NO_ACCESS, but it might be returned for buggy devices.
                        case ErrorGenFailure:
                        case ErrorNoAccess:
                            return PhysicalDescriptorSetCollection.Empty;
                        case ErrorInvalidUserBuffer when length < MaxLength:
                            length = Math.Min(2 * length, MaxLength);
                            continue;
                        case int code:
                            throw new Win32Exception(code);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(data);
                }
            }
        }
    }
}