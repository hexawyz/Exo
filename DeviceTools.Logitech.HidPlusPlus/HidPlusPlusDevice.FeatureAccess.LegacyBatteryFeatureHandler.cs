using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class LegacyBatteryFeatureHandler : BatteryFeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.BatteryUnifiedLevelStatus;

			private byte _levelCount;
			private BatteryUnifiedLevelStatus.BatteryCapabilityFlags _capabilityFlags;

			private uint _batteryLevelAndStatus;

			public override BatteryPowerState PowerState => GetBatteryPowerState(Volatile.Read(ref _batteryLevelAndStatus));

			private static BatteryPowerState GetBatteryPowerState(uint batteryLevelAndStatus)
				=> GetBatteryPowerState((short)batteryLevelAndStatus, (BatteryUnifiedLevelStatus.BatteryStatus)(byte)(batteryLevelAndStatus >> 16));

			private static BatteryPowerState GetBatteryPowerState(short batteryLevel, BatteryUnifiedLevelStatus.BatteryStatus batteryStatus)
			{
				var chargeStatus = BatteryChargeStatus.Discharging;
				var externalPowerStatus = BatteryExternalPowerStatus.None;

				switch (batteryStatus)
				{
				case BatteryUnifiedLevelStatus.BatteryStatus.Discharging:
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.Recharging:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.Charging, BatteryExternalPowerStatus.IsConnected);
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.ChargeInFinalStage:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.ChargingNearlyComplete, BatteryExternalPowerStatus.IsConnected);
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.ChargeComplete:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.ChargingComplete, BatteryExternalPowerStatus.IsConnected);
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.RechargingBelowOptimalSpeed:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.Charging, BatteryExternalPowerStatus.IsConnected | BatteryExternalPowerStatus.IsChargingBelowOptimalSpeed);
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.InvalidBatteryType:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.InvalidBatteryType, BatteryExternalPowerStatus.None);
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.ThermalError:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.BatteryTooHot, BatteryExternalPowerStatus.None);
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.OtherChargingError:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.InvalidBatteryType, BatteryExternalPowerStatus.None);
					break;
				}

				return new((ushort)batteryLevel <= 255 ? (byte)batteryLevel : null, chargeStatus, externalPowerStatus);
			}

			public LegacyBatteryFeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
				_batteryLevelAndStatus = 0xFFFF;
			}

			protected override async Task RefreshBatteryCapabilitiesAsync(int retryCount, CancellationToken cancellationToken)
			{
				var response = await Device.SendWithRetryAsync<BatteryUnifiedLevelStatus.GetBatteryCapability.Response>
				(
					FeatureIndex,
					BatteryUnifiedLevelStatus.GetBatteryCapability.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				_levelCount = response.NumberOfLevels;
				_capabilityFlags = response.Flags;
			}

			protected override async Task RefreshBatteryStatusAsync(int retryCount, CancellationToken cancellationToken)
			{
				var response = await Device.SendWithRetryAsync<BatteryUnifiedLevelStatus.GetBatteryLevelStatus.Response>
				(
					FeatureIndex,
					BatteryUnifiedLevelStatus.GetBatteryLevelStatus.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				ProcessStatusResponse(ref response);
			}

			protected override void HandleNotification(byte eventId, ReadOnlySpan<byte> response)
			{
				if (response.Length < 16) return;

				if (eventId != BatteryUnifiedLevelStatus.GetBatteryLevelStatus.EventId) return;

				ProcessStatusResponse(ref Unsafe.As<byte, BatteryUnifiedLevelStatus.GetBatteryLevelStatus.Response>(ref MemoryMarshal.GetReference(response)));
			}

			private void ProcessStatusResponse(ref BatteryUnifiedLevelStatus.GetBatteryLevelStatus.Response response)
			{
				uint oldBatteryLevelAndStatus;
				uint newBatteryLevelAndStatus;

				lock (this)
				{
					oldBatteryLevelAndStatus = _batteryLevelAndStatus;
					short newBatteryLevel = response.BatteryDischargeLevel;

					// It seems that the charge level can be reported as zero when the device is charging. (Which explains the Windows 0% notification when starting the keyboard plugged)
					// We can try to rely on the battery status to provide a better approximate in some cases.
					if (response.BatteryStatus is BatteryUnifiedLevelStatus.BatteryStatus.ChargeComplete)
					{
						newBatteryLevel = 100;
					}
					else if (response.BatteryStatus is BatteryUnifiedLevelStatus.BatteryStatus.ChargeInFinalStage)
					{
						newBatteryLevel = 90;
					}
					else if (response.BatteryDischargeLevel == 0)
					{
						newBatteryLevel = -1;
					}

					newBatteryLevelAndStatus = (uint)response.BatteryStatus << 16 | (ushort)newBatteryLevel;

					if (newBatteryLevelAndStatus != oldBatteryLevelAndStatus)
					{
						Volatile.Write(ref _batteryLevelAndStatus, newBatteryLevelAndStatus);
					}
				}

				if (newBatteryLevelAndStatus != oldBatteryLevelAndStatus)
				{
					var device = Device;
					if (device.BatteryChargeStateChanged is { } batteryChargeStateChanged)
					{
						_ = Task.Run(() => batteryChargeStateChanged.Invoke(device, GetBatteryPowerState(newBatteryLevelAndStatus)));
					}
				}
			}
		}
	}
}
