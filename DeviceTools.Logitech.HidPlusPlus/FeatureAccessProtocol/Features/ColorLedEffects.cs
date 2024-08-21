using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class ColorLedEffects
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.ColorLedEffects;

	public static class GetInfo
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			public byte ZoneCount;

			private byte _nonVolatileCapabilities0;
			private byte _nonVolatileCapabilities1;

			private byte _extendedCapabilities0;
			private byte _extendedCapabilities1;

			public NonVolatileCapabilities NonVolatileCapabilities
			{
				get => (NonVolatileCapabilities)BigEndian.ReadUInt16(in _nonVolatileCapabilities0);
				set => BigEndian.Write(ref _nonVolatileCapabilities0, (ushort)value);
			}

			public ExtendedCapabilities SupportedEffects
			{
				get => (ExtendedCapabilities)BigEndian.ReadUInt16(_extendedCapabilities0);
				set => BigEndian.Write(ref _extendedCapabilities0, (ushort)value);
			}
		}
	}

	public static class GetZoneInfo
	{
		public const byte FunctionId = 1;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public byte ZoneIndex;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			public byte ZoneIndex;
			private byte _location0;
			private byte _location1;
			public byte EffectCount;
			public EffectPersistenceCapabilities PersistenceCapabilities;

			public EffectZoneLocation Location
			{
				get => (EffectZoneLocation)BigEndian.ReadUInt16(_location0);
				set => BigEndian.Write(ref _location0, (ushort)value);
			}
		}
	}

	public static class GetZoneEffectInfo
	{
		public const byte FunctionId = 2;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public byte ZoneIndex;
			public byte EffectIndex;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			public byte ZoneIndex;
			public byte EffectIndex;
			private byte _effect0;
			private byte _effect1;
			private byte _capabilities0;
			private byte _capabilities1;
			private byte _period0;
			private byte _period1;

			public PredefinedEffect Effect
			{
				get => (PredefinedEffect)BigEndian.ReadUInt16(in _effect0);
				set => BigEndian.Write(ref _effect0, (ushort)value);
			}

			public ushort Capabilities
			{
				get => BigEndian.ReadUInt16(_capabilities0);
				set => BigEndian.Write(ref _capabilities0, value);
			}

			public ushort Period
			{
				get => BigEndian.ReadUInt16(_period0);
				set => BigEndian.Write(ref _period0, value);
			}
		}
	}

	public static class GetSoftwareControl
	{
		public const byte FunctionId = 7;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			private byte _isSoftwareControlEnabled;
			private byte _areSynchronizationEventsEnabled;

			public bool IsSoftwareControlEnabled
			{
				get => _isSoftwareControlEnabled != 0;
				set => _isSoftwareControlEnabled = value ? (byte)1 : (byte)0;
			}

			public bool AreSynchronizationEventsEnabled
			{
				get => _areSynchronizationEventsEnabled != 0;
				set => _areSynchronizationEventsEnabled = value ? (byte)1 : (byte)0;
			}
		}
	}

	public static class SetSoftwareControl
	{
		public const byte FunctionId = 8;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			private byte _isSoftwareControlEnabled;
			private byte _areSynchronizationEventsEnabled;

			public bool IsSoftwareControlEnabled
			{
				get => _isSoftwareControlEnabled != 0;
				set => _isSoftwareControlEnabled = value ? (byte)1 : (byte)0;
			}

			public bool AreSynchronizationEventsEnabled
			{
				get => _areSynchronizationEventsEnabled != 0;
				set => _areSynchronizationEventsEnabled = value ? (byte)1 : (byte)0;
			}
		}
	}

	[Flags]
	public enum NonVolatileCapabilities : ushort
	{
		None = 0x0000,
		BootUpEffect = 0x0001,
		Demo = 0x0002,
		UserDemoMode = 0x0004,
	}

	[Flags]
	public enum ExtendedCapabilities : ushort
	{
		None = 0x0000,
		CanGetZoneEffect = 0x0001,
		CannotGetEffectSettings = 0x0002,
		CanSetLedBinInfo = 0x0004,
		MonochromeOnly = 0x0008,
		// V6
		CannotSynchronizeEffect = 0x0010,
	}

	public enum EffectZoneLocation : ushort
	{
		Primary = 1,
		Logo = 2,
		LeftSide = 3,
		RightSide = 4,
		Combined = 5,
		Primary1 = 6,
		Primary2 = 7,
		Primary3 = 8,
		Primary4 = 9,
		Primary5 = 10,
		Primary6 = 11,
	}

	public enum EffectPersistenceCapabilities : byte
	{
		None = 0,
		AlwaysOn = 1,
		AlwaysOff = 2,

	}

	public enum PredefinedEffect : ushort
	{
		Disabled = 0,
		Fixed = 1,
		LegacyPulse = 2,
		ColorCycle = 3,
		ColorWave = 4,
		Starlight = 5,
		LightOnPress = 6,
		AudioVisualizer = 7,
		BootUp = 8,
		DemoMode = 9,
		Pulse = 10,
		Ripple = 11,
	}

	[Flags]
	public enum FixedColorEffectCapabilities : ushort
	{
		None = 0x0000,
		Capabilities = 0x0001,
		RampUpDown = 0x0002,
		NoEffect = 0x0004,
	}

	[Flags]
	public enum ColorCyclingEffectCapabilities : ushort
	{
		None = 0x0000,
		Capabilities = 0x0001,
		Speed = 0x0002,
		Intensity = 0x0004,
		Synchronize = 0x4000,
		Period = 0x8000,
	}

	[Flags]
	public enum ColorWaveEffectCapabilities : ushort
	{
		None = 0x0000,
		Capabilities = 0x0001,
		StartColor = 0x0002,
		StopColor = 0x0004,
		Speed = 0x0008,
		Intensity = 0x0010,
		HorizontalDirection = 0x0020,
		VerticalDirection = 0x0040,
		CenterOutDirection = 0x0080,
		InwardDirection = 0x0100,
		OutwardDirection = 0x0200,
		ReverseHorizontalDirection = 0x0400,
		ReverseVerticalDirection = 0x0800,
		CenterInDirection = 0x1000,
		Synchronize = 0x4000,
		Period = 0x8000,
	}

	[Flags]
	public enum PulseEffectCapabilities : ushort
	{
		None = 0x0000,
		Capabilities = 0x0001,
		Speed = 0x0002,
		Intensity = 0x0004,
		SineWaveform = 0x0008,
		SquareWaveform = 0x0010,
		TriangleWaveform = 0x0020,
		SawToothWaveform = 0x0040,
		SharkFinWaveform = 0x0080,
		Exponential = 0x0100,
		Synchronize = 0x4000,
		Period = 0x8000,
	}

	[Flags]
	public enum RippleEffectCapabilities : ushort
	{
		None = 0x0000,
		Supported = 0x0001,
		Speed = 0x0002,
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
