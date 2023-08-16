using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class UnifiedBattery
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.UnifiedBattery;

	public static class GetCapabilities
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			private byte _supportedBatteryLevels;
			public BatteryLevels SupportedBatteryLevels
			{
				get => (BatteryLevels)_supportedBatteryLevels;
				set => _supportedBatteryLevels = (byte)value;
			}

			private byte _batteryFlags;
			public BatteryFlags BatteryFlags
			{
				get => (BatteryFlags)_batteryFlags;
				set => _batteryFlags = (byte)value;
			}

			private byte _unused;
		}
	}

	public static class GetStatus
	{
		public const byte EventId = 0;
		public const byte FunctionId = 1;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			public byte StateOfCharge;

			private byte _batteryLevel;
			public BatteryLevels BatteryLevel
			{
				get => (BatteryLevels)_batteryLevel;
				set => _batteryLevel = (byte)value;
			}

			private byte _chargingStatus;
			public ChargingStatus ChargingStatus
			{
				get => (ChargingStatus)_chargingStatus;
				set => _chargingStatus = (byte)value;
			}

			private byte _hasExternalPower;
			public bool HasExternalPower
			{
				get => (_hasExternalPower & 1) != 0;
				set => _hasExternalPower = (byte)(value ? _hasExternalPower | 1 : _hasExternalPower & 0xFE);
			}
		}
	}

	[Flags]
	public enum BatteryLevels : byte
	{
		None = 0,
		Critical = 1,
		Low = 2,
		Good = 4,
		Full = 8,
	}

	[Flags]
	public enum BatteryFlags : byte
	{
		None = 0,
		Rechargeable = 1,
		StateOfCharge = 2,
	}

	public enum ChargingStatus : byte
	{
		Discharging = 0,
		Charging = 1,
		SlowCharging = 2,
		ChargingComplete = 3,
		ChargingError = 4,
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
