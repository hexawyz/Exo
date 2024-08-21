using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class UnifiedBatteryFeatureHandler : BatteryFeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.UnifiedBattery;

			private UnifiedBattery.BatteryLevels _supportedBatteryLevels;
			private UnifiedBattery.BatteryFlags _batteryFlags;

			private uint _batteryLevelAndStatus;

			public override BatteryPowerState PowerState => GetBatteryPowerState(Volatile.Read(ref _batteryLevelAndStatus));

			private BatteryPowerState GetBatteryPowerState(uint batteryLevelAndStatus)
				=> GetBatteryPowerState
				(
					(byte)batteryLevelAndStatus,
					(UnifiedBattery.BatteryLevels)(byte)(batteryLevelAndStatus >> 8),
					(UnifiedBattery.ChargingStatus)(byte)(batteryLevelAndStatus >> 16),
					((byte)(batteryLevelAndStatus >> 24) & 1) != 0
				);

			private BatteryPowerState GetBatteryPowerState
			(
				byte batteryLevelPercentage,
				UnifiedBattery.BatteryLevels batteryLevel,
				UnifiedBattery.ChargingStatus chargingStatus,
				bool isExternalPowerConnected
			)
			{
				byte rawBatteryLevel = 0;

				if ((_batteryFlags & UnifiedBattery.BatteryFlags.StateOfCharge) != 0)
				{
					rawBatteryLevel = batteryLevelPercentage;
				}
				else
				{
					if ((batteryLevel & UnifiedBattery.BatteryLevels.Full) != 0)
					{
						rawBatteryLevel = 100;
					}
					else if ((batteryLevel & UnifiedBattery.BatteryLevels.Good) != 0)
					{
						rawBatteryLevel = 60;
					}
					else if ((batteryLevel & UnifiedBattery.BatteryLevels.Low) != 0)
					{
						rawBatteryLevel = 20;
					}
					else if ((batteryLevel & UnifiedBattery.BatteryLevels.Critical) != 0)
					{
						rawBatteryLevel = 10;
					}
				}

				var chargeStatus = BatteryChargeStatus.Discharging;
				var externalPowerStatus = isExternalPowerConnected ? BatteryExternalPowerStatus.IsConnected : BatteryExternalPowerStatus.None;

				switch (chargingStatus)
				{
				case UnifiedBattery.ChargingStatus.Discharging:
					chargeStatus = BatteryChargeStatus.Discharging;
					break;
				case UnifiedBattery.ChargingStatus.Charging:
					chargeStatus = BatteryChargeStatus.Charging;
					break;
				case UnifiedBattery.ChargingStatus.SlowCharging:
					chargeStatus = BatteryChargeStatus.Charging; // TODO: Is it slow charging or close to completion ?
					externalPowerStatus |= BatteryExternalPowerStatus.IsChargingBelowOptimalSpeed;
					break;
				case UnifiedBattery.ChargingStatus.ChargingComplete:
					chargeStatus = BatteryChargeStatus.ChargingComplete;
					break;
				case UnifiedBattery.ChargingStatus.ChargingError:
					chargeStatus = BatteryChargeStatus.ChargingError;
					break;
				}

				return new(rawBatteryLevel, chargeStatus, externalPowerStatus);
			}

			public UnifiedBatteryFeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex) { }

			protected override async Task RefreshBatteryCapabilitiesAsync(int retryCount, CancellationToken cancellationToken)
			{
				var response = await Device.SendWithRetryAsync<UnifiedBattery.GetCapabilities.Response>
				(
					FeatureIndex,
					UnifiedBattery.GetCapabilities.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				_supportedBatteryLevels = response.SupportedBatteryLevels;
				_batteryFlags = response.BatteryFlags;
			}

			protected override async Task RefreshBatteryStatusAsync(int retryCount, CancellationToken cancellationToken)
			{
				var response = await Device.SendWithRetryAsync<UnifiedBattery.GetStatus.Response>
				(
					FeatureIndex,
					UnifiedBattery.GetStatus.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				ProcessStatusResponse(ref response);
			}

			protected override void HandleNotification(byte eventId, ReadOnlySpan<byte> response)
			{
				if (response.Length < 16) return;

				if (eventId != UnifiedBattery.GetStatus.EventId) return;

				ProcessStatusResponse(ref Unsafe.As<byte, UnifiedBattery.GetStatus.Response>(ref MemoryMarshal.GetReference(response)));
			}

			private void ProcessStatusResponse(ref UnifiedBattery.GetStatus.Response response)
			{
				uint oldBatteryLevelAndStatus;
				uint newBatteryLevelAndStatus;

				lock (this)
				{
					oldBatteryLevelAndStatus = _batteryLevelAndStatus;
					newBatteryLevelAndStatus = response.StateOfCharge | (uint)response.BatteryLevel << 8 | (uint)response.ChargingStatus << 16 | (response.HasExternalPower ? 1U << 24 : 0);

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
