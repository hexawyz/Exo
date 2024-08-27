using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using DeviceTools.HumanInterfaceDevices.Usages;
using DeviceTools.Logitech.HidPlusPlus.LedEffects;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

// NB: Information mostly obtained from libratbag, as logitech doesn't seem to have published an official doc ?
// See: https://github.com/libratbag/libratbag

#pragma warning disable IDE0044 // Add readonly modifier
public static class OnBoardProfiles
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.ColorLedEffects;

	public static class GetInfo
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			public MemoryType MemoryType;
			public ProfileFormat ProfileFormat;
			public MacroFormat MacroFormat;
			public byte ProfileCount;
			public byte RomProfileCount;
			public byte ButtonCount;
			public byte SectorCount;
			private byte _sectorSize0;
			private byte _sectorSize1;
			public byte MechanicalLayout;
			public byte DeviceConnectionMode;

			public ushort SectorSize
			{
				get => BigEndian.ReadUInt16(in _sectorSize0);
				set => BigEndian.Write(ref _sectorSize0, value);
			}

			public bool HasGShift => (MechanicalLayout & 0x03) is 0x02;
			public bool HasDpiShift => (MechanicalLayout & 0x0C) is 0x80;

			public bool IsCorded => (DeviceConnectionMode & 0x07) is 0x01 or 0x04;
			public bool IsWireless => (DeviceConnectionMode & 0x07) is 0x02 or 0x04;
		}
	}

	public static class SetDeviceMode
	{
		public const byte FunctionId = 1;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public DeviceMode Mode;
		}
	}

	public static class GetDeviceMode
	{
		public const byte FunctionId = 2;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public DeviceMode Mode;
		}
	}

	public static class SetCurrentProfile
	{
		public const byte FunctionId = 3;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			private byte _unknown;
			public byte ActiveProfileIndex;
		}
	}

	public static class GetCurrentProfile
	{
		public const byte FunctionId = 4;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			private byte _unknown;
			public byte ActiveProfileIndex;
		}
	}

	public static class ReadMemory
	{
		public const byte FunctionId = 5;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Request : IMessageRequestParameters, ILongMessageParameters
		{
			private byte _sectorIndex0;
			private byte _sectorIndex1;
			private byte _offset0;
			private byte _offset1;

			public ushort SectorIndex
			{
				get => BigEndian.ReadUInt16(in _sectorIndex0);
				set => BigEndian.Write(ref _sectorIndex0, value);
			}

			public ushort Offset
			{
				get => BigEndian.ReadUInt16(in _offset0);
				set => BigEndian.Write(ref _offset0, value);
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			private byte _data0;
			private byte _data1;
			private byte _data2;
			private byte _data3;
			private byte _data4;
			private byte _data5;
			private byte _data6;
			private byte _data7;
			private byte _data8;
			private byte _data9;
			private byte _dataA;
			private byte _dataB;
			private byte _dataC;
			private byte _dataD;
			private byte _dataE;
			private byte _dataF;

			public static ReadOnlySpan<byte> AsReadOnlySpan(in Response response)
				=> MemoryMarshal.CreateReadOnlySpan(in response._data0, 16);

			public readonly void CopyTo(Span<byte> span)
				=> AsReadOnlySpan(in this).CopyTo(span);
		}
	}

	public static class StartWrite
	{
		public const byte FunctionId = 6;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Request : IMessageRequestParameters, ILongMessageParameters
		{
			private byte _sectorIndex0;
			private byte _sectorIndex1;
			private byte _offset0;
			private byte _offset1;
			private byte _count0;
			private byte _count1;

			public ushort SectorIndex
			{
				get => BigEndian.ReadUInt16(in _sectorIndex0);
				set => BigEndian.Write(ref _sectorIndex0, value);
			}

			public ushort Offset
			{
				get => BigEndian.ReadUInt16(in _offset0);
				set => BigEndian.Write(ref _offset0, value);
			}

			public ushort Count
			{
				get => BigEndian.ReadUInt16(in _count0);
				set => BigEndian.Write(ref _count0, value);
			}
		}
	}

	public static class WriteMemory
	{
		public const byte FunctionId = 7;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Request : IMessageRequestParameters, ILongMessageParameters
		{
			private byte _data0;
			private byte _data1;
			private byte _data2;
			private byte _data3;
			private byte _data4;
			private byte _data5;
			private byte _data6;
			private byte _data7;
			private byte _data8;
			private byte _data9;
			private byte _dataA;
			private byte _dataB;
			private byte _dataC;
			private byte _dataD;
			private byte _dataE;
			private byte _dataF;

			public static Span<byte> AsSpan(ref Request request)
				=> MemoryMarshal.CreateSpan(ref request._data0, 16);

			public void Write(ReadOnlySpan<byte> span)
				=> span.CopyTo(AsSpan(ref this));
		}
	}

	public static class EndWrite
	{
		public const byte FunctionId = 8;
	}

	public static class GetCurrentDpiIndex
	{
		public const byte FunctionId = 11;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public byte ActivePresetIndex;
		}
	}

	public static class SetCurrentDpiIndex
	{
		public const byte FunctionId = 12;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public byte ActivePresetIndex;
		}
	}

	public enum MemoryType : byte
	{
		G402 = 1,
	}

	public enum ProfileFormat : byte
	{
		G402 = 1,
		G303 = 2,
		G900 = 3,
		G915 = 4,
	}

	public enum MacroFormat : byte
	{
		G402 = 1,
	}

	public readonly struct ProfileEntry
	{
		public static readonly ProfileEntry Empty = Unsafe.BitCast<uint, ProfileEntry>(0xFFFFFFFF);

		// NB: libratbag calls those 16 bytes the address, but it is correlated with the 1-based index?
		private readonly byte _reserved0;
		private readonly byte _profileIndex;
		private readonly byte _isEnabled;
		private readonly byte _reserved;

		public byte ProfileIndex => _profileIndex;
		public bool IsEnabled => _isEnabled != 0;

		public ProfileEntry(byte profileIndex, bool isEnabled)
		{
			_profileIndex = profileIndex;
			_isEnabled = isEnabled ? (byte)1 : (byte)0;
			_reserved = 0xFF;
		}
	}

	public struct Profile
	{
		public byte PollingRateDivider;
		public byte DefaultDpiIndex;
		public byte SwitchedDpiIndex;
		public ProfileDpiCollection DpiPresets;
		public Color ProfileColor;
		public byte PowerMode;
		public byte AngleSnapping;
		private byte _reserved0;
		private byte _reserved1;
		private byte _reserved2;
		private byte _reserved3;
		private byte _reserved4;
		private byte _reserved5;
		private byte _reserved6;
		private byte _reserved7;
		private byte _reserved8;
		private byte _reserved9;
		private byte _lowPowerModeDelay0;
		private byte _lowPowerModeDelay1;
		private byte _powerOffDelay0;
		private byte _powerOffDelay1;
		public ButtonConfigurationCollection Buttons;
		public ButtonConfigurationCollection AlternateButtons;
		public ProfileName Name;
		public LedEffectCollection Leds;
		public LedEffectCollection AlternateLeds;
	}

	[InlineArray(48)]
	public struct ProfileName
	{
		private byte _element0;

		private static string ToString(ReadOnlySpan<byte> span)
		{
			int endIndex = span.IndexOf((byte)0);
			return Encoding.UTF8.GetString(endIndex >= 0 ? span[..endIndex] : span);
		}

		public override string ToString()
			=> ToString(this);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 10)]
	[DebuggerDisplay("{this[0],d}, {this[1],d}, {this[2],d}, {this[3],d}, {this[4],d}")]
	public readonly struct ProfileDpiCollection : IReadOnlyList<ushort>
	{
		public struct Enumerator : IEnumerator<ushort>
		{
			private int _index;
			private readonly ProfileDpiCollection _dpiCollection;

			internal Enumerator(in ProfileDpiCollection dpiCollection)
			{
				_index = -1;
				_dpiCollection = dpiCollection;
			}

			readonly void IDisposable.Dispose() { }

			public readonly ushort Current => _dpiCollection[_index];
			readonly object IEnumerator.Current => Current;

			public bool MoveNext() => (uint)++_index < (uint)_dpiCollection.Count;
			void IEnumerator.Reset() => _index = -1;
		}

		private readonly byte _dpi00;
		private readonly byte _dpi01;
		private readonly byte _dpi10;
		private readonly byte _dpi11;
		private readonly byte _dpi20;
		private readonly byte _dpi21;
		private readonly byte _dpi30;
		private readonly byte _dpi31;
		private readonly byte _dpi40;
		private readonly byte _dpi41;

		public ushort this[int index]
		{
			get
			{
				if ((uint)index > 4) throw new ArgumentOutOfRangeException(nameof(index));

				return LittleEndian.ReadUInt16(in Unsafe.AddByteOffset(ref Unsafe.AsRef(in _dpi00), 2 * index));
			}
			init
			{
				if ((uint)index > 4) throw new ArgumentOutOfRangeException(nameof(index));

				LittleEndian.Write(ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _dpi00), 2 * index), value);
			}
		}

		public int Count => 5;

		public Enumerator GetEnumerator() => new(in this);
		IEnumerator<ushort> IEnumerable<ushort>.GetEnumerator() => GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
	[DebuggerDisplay("Count = {Count}")]
	public readonly struct ButtonConfigurationCollection : IReadOnlyList<ButtonConfiguration>
	{
		public struct Enumerator : IEnumerator<ButtonConfiguration>
		{
			private int _index;
			private readonly ButtonConfigurationCollection _collection;

			internal Enumerator(in ButtonConfigurationCollection collection)
			{
				_index = -1;
				_collection = collection;
			}

			void IDisposable.Dispose() { }

			public ButtonConfiguration Current => _collection[_index];
			object IEnumerator.Current => Current;

			public bool MoveNext() => (uint)++_index < (uint)_collection.Count;
			void IEnumerator.Reset() => _index = -1;
		}

		private readonly ButtonConfiguration _button0;
		private readonly ButtonConfiguration _button1;
		private readonly ButtonConfiguration _button2;
		private readonly ButtonConfiguration _button3;
		private readonly ButtonConfiguration _button4;
		private readonly ButtonConfiguration _button5;
		private readonly ButtonConfiguration _button6;
		private readonly ButtonConfiguration _button7;
		private readonly ButtonConfiguration _button8;
		private readonly ButtonConfiguration _button9;
		private readonly ButtonConfiguration _buttonA;
		private readonly ButtonConfiguration _buttonB;
		private readonly ButtonConfiguration _buttonC;
		private readonly ButtonConfiguration _buttonD;
		private readonly ButtonConfiguration _buttonE;
		private readonly ButtonConfiguration _buttonF;

		public ButtonConfiguration this[int index]
		{
			get
			{
				if ((uint)index > 16) throw new ArgumentOutOfRangeException(nameof(index));

				return Unsafe.ReadUnaligned<ButtonConfiguration>(in Unsafe.AddByteOffset(ref Unsafe.As<ButtonConfiguration, byte>(ref Unsafe.AsRef(in _button0)), 4 * index));
			}
			init
			{
				if ((uint)index > 16) throw new ArgumentOutOfRangeException(nameof(index));

				Unsafe.WriteUnaligned(ref Unsafe.AddByteOffset(ref Unsafe.As<ButtonConfiguration, byte>(ref Unsafe.AsRef(in _button0)), 4 * index), value);
			}
		}

		public int Count => 16;

		public Enumerator GetEnumerator() => new(in this);
		IEnumerator<ButtonConfiguration> IEnumerable<ButtonConfiguration>.GetEnumerator() => GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
	[DebuggerDisplay("{DebugView}")]
	public readonly struct ButtonConfiguration
	{
		public static readonly ButtonConfiguration Disabled = new(ButtonType.Disabled, 0, 0, 0);

		public ButtonType Type { get; }
		private readonly byte _data0;
		private readonly byte _data1;
		private readonly byte _data2;

		private ButtonConfiguration(ButtonType type, byte data0, byte data1, byte data2)
			=> (Type, _data0, _data1, _data2) = (type, data0, data1, data2);

		public MacroButtonConfiguration AsMacro() => Type == ButtonType.Macro ? Unsafe.BitCast<ButtonConfiguration, MacroButtonConfiguration>(this) : throw new InvalidCastException();
		public HidButtonConfiguration AsHid() => Type == ButtonType.Hid ? Unsafe.BitCast<ButtonConfiguration, HidButtonConfiguration>(this) : throw new InvalidCastException();
		public SpecialButtonConfiguration AsSpecial() => Type == ButtonType.Special ? Unsafe.BitCast<ButtonConfiguration, SpecialButtonConfiguration>(this) : throw new InvalidCastException();

		private object DebugView
			=> Type switch
			{
				ButtonType.Macro => AsMacro(),
				ButtonType.Hid => AsHid(),
				ButtonType.Special => AsSpecial(),
				_ => Type
			};
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
	[DebuggerDisplay("{Type}, Page = {Page,h}, Offset = {Offset,h}")]
	public readonly struct MacroButtonConfiguration
	{
		public ButtonType Type { get; }
		public byte Page { get; }
		private readonly byte _reserved;
		public byte Offset { get; }

		public static implicit operator ButtonConfiguration(MacroButtonConfiguration value) => Unsafe.BitCast<MacroButtonConfiguration, ButtonConfiguration>(value);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
	[DebuggerDisplay("{DebugView}")]
	public readonly struct HidButtonConfiguration
	{
		public ButtonType Type { get; }
		public ButtonHidCategory HidCategory { get; }
		private readonly byte _data0;
		private readonly byte _data1;

		public MouseButtonConfiguration AsMouse() => HidCategory == ButtonHidCategory.Mouse ? Unsafe.BitCast<HidButtonConfiguration, MouseButtonConfiguration>(this) : throw new InvalidCastException();
		public KeyboardButtonConfiguration AsKeyboard() => HidCategory == ButtonHidCategory.Keyboard ? Unsafe.BitCast<HidButtonConfiguration, KeyboardButtonConfiguration>(this) : throw new InvalidCastException();
		public ConsumerControlButtonConfiguration AsConsumerControl() => HidCategory == ButtonHidCategory.ConsumerControl ? Unsafe.BitCast<HidButtonConfiguration, ConsumerControlButtonConfiguration>(this) : throw new InvalidCastException();

		public static implicit operator ButtonConfiguration(HidButtonConfiguration value) => Unsafe.BitCast<HidButtonConfiguration, ButtonConfiguration>(value);

		private object DebugView
			=> HidCategory switch
			{
				ButtonHidCategory.Mouse => AsMouse(),
				ButtonHidCategory.Keyboard => AsKeyboard(),
				ButtonHidCategory .ConsumerControl => AsConsumerControl(),
				_ => HidCategory
			};
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
	[DebuggerDisplay("{Type}, HidCategory = {HidCategory,h}, Buttons = {Buttons,h}")]
	public readonly struct MouseButtonConfiguration
	{
		public ButtonType Type { get; }
		public ButtonHidCategory HidCategory { get; }

		private readonly byte _buttons0;
		private readonly byte _buttons1;

		public MouseButtons Buttons => (MouseButtons)BigEndian.ReadUInt16(in _buttons0);

		public MouseButtonConfiguration(MouseButtons buttons)
		{
			if (buttons == 0) throw new ArgumentOutOfRangeException(nameof(buttons));
			Type = ButtonType.Hid;
			HidCategory = ButtonHidCategory.Mouse;
			BigEndian.Write(ref _buttons0, (ushort)buttons);
		}

		public static implicit operator ButtonConfiguration(MouseButtonConfiguration value) => Unsafe.BitCast<MouseButtonConfiguration, ButtonConfiguration>(value);
		public static implicit operator HidButtonConfiguration(MouseButtonConfiguration value) => Unsafe.BitCast<MouseButtonConfiguration, HidButtonConfiguration>(value);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
	[DebuggerDisplay("{Type}, HidCategory = {HidCategory,h}, Modifiers = {Modifiers,h}, Usage = {Usage,h}")]
	public readonly struct KeyboardButtonConfiguration
	{
		public ButtonType Type { get; }
		public ButtonHidCategory HidCategory { get; }
		public KeyboardButtonModifiers Modifiers { get; }
		private readonly byte _usage;
		public HidKeyboardUsage Usage => (HidKeyboardUsage)_usage;

		public KeyboardButtonConfiguration(KeyboardButtonModifiers modifiers, HidKeyboardUsage usage)
		{
			if ((modifiers & (KeyboardButtonModifiers.Control | KeyboardButtonModifiers.Shift)) != 0) throw new ArgumentOutOfRangeException(nameof(modifiers));
			if (usage == 0 || (byte)usage > 0xFF) throw new ArgumentOutOfRangeException(nameof(usage));
			Type = ButtonType.Hid;
			HidCategory = ButtonHidCategory.Keyboard;
			_usage = (byte)(ushort)usage;
		}

		public static implicit operator ButtonConfiguration(KeyboardButtonConfiguration value) => Unsafe.BitCast<KeyboardButtonConfiguration, ButtonConfiguration>(value);
		public static implicit operator HidButtonConfiguration(KeyboardButtonConfiguration value) => Unsafe.BitCast<KeyboardButtonConfiguration, HidButtonConfiguration>(value);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
	[DebuggerDisplay("{Type}, HidCategory = {HidCategory,h}, Usage = {Usage,h}")]
	public readonly struct ConsumerControlButtonConfiguration
	{
		public ButtonType Type { get; }
		public ButtonHidCategory HidCategory { get; }

		private readonly byte _usage0;
		private readonly byte _usage1;

		public HidConsumerUsage Usage => (HidConsumerUsage)BigEndian.ReadUInt16(in _usage0);

		public ConsumerControlButtonConfiguration(HidConsumerUsage usage)
		{
			if (usage == 0) throw new ArgumentOutOfRangeException(nameof(usage));
			Type = ButtonType.Hid;
			HidCategory = ButtonHidCategory.ConsumerControl;
			BigEndian.Write(ref _usage0, (ushort)usage);
		}

		public static implicit operator ButtonConfiguration(ConsumerControlButtonConfiguration value) => Unsafe.BitCast<ConsumerControlButtonConfiguration, ButtonConfiguration>(value);
		public static implicit operator HidButtonConfiguration(ConsumerControlButtonConfiguration value) => Unsafe.BitCast<ConsumerControlButtonConfiguration, HidButtonConfiguration>(value);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
	[DebuggerDisplay("{Type}, Button = {Button,h}")]
	public readonly struct SpecialButtonConfiguration
	{
		public ButtonType Type { get; }
		public SpecialButton Button { get; }
		private readonly byte _reserved;
		public readonly byte ProfileIndex { get; }

		public SpecialButtonConfiguration(SpecialButton button)
		{
			if (button == 0) throw new ArgumentOutOfRangeException(nameof(button));
			Type = ButtonType.Special;
			Button = button;
		}

		public SpecialButtonConfiguration(SpecialButton button, byte profileIndex)
		{
			if (button == 0) throw new ArgumentOutOfRangeException(nameof(button));
			Type = ButtonType.Special;
			Button = button;
			ProfileIndex = profileIndex;
		}

		public static implicit operator ButtonConfiguration(SpecialButtonConfiguration value) => Unsafe.BitCast<SpecialButtonConfiguration, ButtonConfiguration>(value);
	}

	public enum ButtonType : byte
	{
		Macro = 0x00,
		Hid = 0x80,
		Special = 0x90,
		Disabled = 0xFF,
	}

	public enum ButtonHidCategory : byte
	{
		None = 0x00,
		Mouse = 0x01,
		Keyboard = 0x02,
		ConsumerControl = 0x03,
	}

	[Flags]
	public enum KeyboardButtonModifiers : byte
	{
		None = 0x00,
		Control = 0x01,
		Shift = 0x02,
	}

	public enum SpecialButton : byte
	{
		None = 0x00,
		TiltLeft = 0x01,
		TiltRight = 0x02,
		NextDpi = 0x03,
		PreviousDpi = 0x04,
		CycleDpi = 0x05,
		DefaultDpi = 0x06,
		ShiftDpi = 0x07,
		NextProfile = 0x08,
		PreviousProfile = 0x09,
		CycleProfile = 0x0A,
		GShift = 0x0B,

		BatteryIndicator = 0x0C,
		EnableProfile = 0x0D,
		PerformanceSwitch = 0x0E,
		Host = 0x0F,
		ScrollDown = 0x10,
		ScrollUp = 0x11,
	}

	[Flags]
	public enum MouseButtons : ushort
	{
		LeftButton = 0x0001,
		RightButton = 0x0002,
		MiddleButton = 0x0004,
		Button4 = 0x0008,
		Button5 = 0x0010,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 22)]
	[DebuggerDisplay("Count = {Count}")]
	public readonly struct LedEffectCollection : IReadOnlyList<LedEffect>
	{
		public struct Enumerator : IEnumerator<LedEffect>
		{
			private int _index;
			private readonly LedEffectCollection _collection;

			internal Enumerator(in LedEffectCollection collection)
			{
				_index = -1;
				_collection = collection;
			}

			void IDisposable.Dispose() { }

			public LedEffect Current => _collection[_index];
			object IEnumerator.Current => Current;

			public bool MoveNext() => (uint)++_index < (uint)_collection.Count;
			void IEnumerator.Reset() => _index = -1;
		}

		private readonly LedEffect _led0;
		private readonly LedEffect _led1;

		public LedEffect this[int index]
		{
			get
			{
				if ((uint)index > 2) throw new ArgumentOutOfRangeException(nameof(index));

				return Unsafe.ReadUnaligned<LedEffect>(in Unsafe.AddByteOffset(ref Unsafe.As<LedEffect, byte>(ref Unsafe.AsRef(in _led0)), 4 * index));
			}
			init
			{
				if ((uint)index > 2) throw new ArgumentOutOfRangeException(nameof(index));

				Unsafe.WriteUnaligned(ref Unsafe.AddByteOffset(ref Unsafe.As<LedEffect, byte>(ref Unsafe.AsRef(in _led0)), 4 * index), value);
			}
		}

		public int Count => 2;

		public Enumerator GetEnumerator() => new(in this);
		IEnumerator<LedEffect> IEnumerable<LedEffect>.GetEnumerator() => GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
