using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using DeviceTools.HumanInterfaceDevices;

namespace DeviceTools.RawInput
{
	[SuppressUnmanagedCodeSecurity]
	public static class NativeMethods
	{
		private const int ErrorGenFailure = 0x0000001F;
		private const int ErrorInsufficientBuffer = 0x0000007A;
		private const int ErrorInvalidUserBuffer = 0x000006F8;
		private const int ErrorNoAccess = 0x000003E6;
		public const int ErrorNoMoreItems = 0x00000103;

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
			public IntPtr WParam;
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct RawMouse
		{
			///<summary>Indicator flags.</summary>
			[FieldOffset(0)]
			public ushort Flags;

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
	}
}
