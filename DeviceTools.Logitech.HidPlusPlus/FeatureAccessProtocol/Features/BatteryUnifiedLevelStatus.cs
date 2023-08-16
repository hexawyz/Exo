using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class BatteryUnifiedLevelStatus
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.BatteryUnifiedLevelStatus;

	public static class GetBatteryLevelStatus
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public byte BatteryDischargeLevel;
			public byte BatteryDischargeNextLevel;

			private byte _batteryStatus;
			public BatteryStatus BatteryStatus
			{
				get => (BatteryStatus)_batteryStatus;
				set => _batteryStatus = (byte)value;
			}
		}
	}

	public static class GetBatteryCapability
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			public byte NumberOfLevels;
			public byte Flags;
			public byte BatteryStatus;

			private byte _nominalBatteryLife0;
			private byte _nominalBatteryLife1;
			public ushort FeatureId
			{
				get => BigEndian.ReadUInt16(_nominalBatteryLife0);
				set => BigEndian.Write(ref _nominalBatteryLife0, (ushort)value);
			}

			public byte BatteryCriticalLevel;
		}
	}

	public enum BatteryStatus : byte
	{
		Discharging = 0,
		Recharging = 1,
		ChargeInFinalStage = 2,
		ChargeComplete = 3,
		RechargingBelowOptimalSpeed = 4,
		InvalidBatteryType = 5,
		ThermalError = 6,
		OtherChargingError = 7,
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
