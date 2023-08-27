using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class BacklightV2
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.BacklightV2;

	public static class GetBacklightConfig
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			private byte _backlightEnable;

			public bool BacklightEnable
			{
				get => (_backlightEnable & 1) != 0;
				set => _backlightEnable = value ? (byte)(_backlightEnable | 1) : (byte)(_backlightEnable & ~1);
			}
			private byte _enabledOptions;

			public BacklightOptions EnabledOptions
			{
				get => (BacklightOptions)_enabledOptions;
				set => _enabledOptions = (byte)value;
			}

			private byte _supportedOptions;

			public BacklightOptions SupportedOptions
			{
				get => (BacklightOptions)_supportedOptions;
				set => _supportedOptions = (byte)value;
			}

			private byte _backlightEffectList0;
			private byte _backlightEffectList1;

			public BacklightEffects SupportedEffects
			{
				get => (BacklightEffects)BigEndian.ReadUInt16(_backlightEffectList0);
				set => BigEndian.Write(ref _backlightEffectList0, (ushort)value);
			}
		}
	}

	public static class GetBacklightInfo
	{
		public const byte EventId = 0;
		public const byte FunctionId = 2;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			public byte LevelCount;

			public byte CurrentLevel;

			private byte _backlightStatus;

			public BacklightStatus BacklightStatus
			{
				get => (BacklightStatus)_backlightStatus;
				set => _backlightStatus = (byte)value;
			}

			private byte _backlightEffect;

			public BacklightEffect BacklightEffect
			{
				get => (BacklightEffect)_backlightStatus;
				set => _backlightStatus = (byte)value;
			}
		}
	}

	[Flags]
	public enum BacklightStatus : byte
	{
		DisabledBySoftware = 0,
		DisabledByCriticalBattery = 1,
		AutomaticMode = 2,
		AutomaticModeSaturated = 3,
		ManualMode = 4,
	}

	[Flags]
	public enum BacklightOptions : byte
	{
		WowEffect = 0x01,
		CrownEffect = 0x02,
		PowerSave = 0x04,
	}

	[Flags]
	public enum BacklightEffects : ushort
	{
		Static = 0x01,
		None = 0x02,
		BreathingLight = 0x04,
		Contrast = 0x08,
		Reaction = 0x10,
		Random = 0x20,
		Waves = 0x40,
	}

	public enum BacklightEffect : byte
	{
		Static = 0x00,
		None = 0x01,
		BreathingLight = 0x02,
		Contrast = 0x03,
		Reaction = 0x04,
		Random = 0x05,
		Waves = 0x06,
		DoNotChange = 0xFF,
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
