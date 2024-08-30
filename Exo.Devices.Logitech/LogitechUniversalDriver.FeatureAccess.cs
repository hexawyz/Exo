using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus;
using Exo.Features;
using Exo.Features.Keyboards;
using Exo.Features.Mouses;
using Exo.Features.PowerManagement;
using Microsoft.Extensions.Logging;
using BacklightState = Exo.Features.Keyboards.BacklightState;

namespace Exo.Devices.Logitech;

public abstract partial class LogitechUniversalDriver
{
	private abstract class FeatureAccess :
		LogitechUniversalDriver,
		IBatteryStateDeviceFeature,
		IKeyboardBacklightFeature,
		IKeyboardLockKeysFeature,
		IMouseDpiFeature,
		IMouseDynamicDpiFeature,
		IMouseDpiPresetsFeature,
		IMouseConfigurableDpiPresetsFeature,
		IMouseConfigurablePollingFrequencyFeature
	{
		public FeatureAccess(HidPlusPlusDevice.FeatureAccess device, ILogger<FeatureAccess> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
			: base(device, logger, configurationKey, versionNumber)
		{
			if (HasBattery) device.BatteryChargeStateChanged += OnBatteryChargeStateChanged;

			if (HasBacklight) device.BacklightStateChanged += OnBacklightStateChanged;

			if (HasLockKeys) device.LockKeysChanged += OnLockKeysChanged;

			if (HasAdjustableDpi) device.DpiChanged += OnDpiChanged;
		}

		protected override IDeviceFeatureSet<IPowerManagementDeviceFeature> CreatePowerManagementDeviceFeatures()
			=> HasBattery ?
				FeatureSet.Create<IPowerManagementDeviceFeature, FeatureAccess, IBatteryStateDeviceFeature>(this) :
				FeatureSet.Empty<IPowerManagementDeviceFeature>();

		public override ValueTask DisposeAsync()
		{
			Device.BatteryChargeStateChanged -= OnBatteryChargeStateChanged;
			return base.DisposeAsync();
		}

		protected IDeviceFeatureSet<IMouseDeviceFeature> CreateMouseFeatures()
			=> HasAdjustableDpi ?
				HasOnBoardProfiles ?
					HasAdjustableReportInterval ?
						FeatureSet.Create<IMouseDeviceFeature, FeatureAccess, IMouseDpiFeature, IMouseDynamicDpiFeature, IMouseDpiPresetsFeature, IMouseConfigurablePollingFrequencyFeature>(this) :
						FeatureSet.Create<IMouseDeviceFeature, FeatureAccess, IMouseDpiFeature, IMouseDynamicDpiFeature, IMouseDpiPresetsFeature>(this) :
					HasAdjustableReportInterval ?
						FeatureSet.Create<IMouseDeviceFeature, FeatureAccess, IMouseDpiFeature, IMouseDynamicDpiFeature, IMouseConfigurablePollingFrequencyFeature>(this) :
						FeatureSet.Create<IMouseDeviceFeature, FeatureAccess, IMouseDpiFeature, IMouseDynamicDpiFeature>(this) :
				HasAdjustableReportInterval ?
					FeatureSet.Create<IMouseDeviceFeature, FeatureAccess, IMouseConfigurablePollingFrequencyFeature>(this) :
					FeatureSet.Empty<IMouseDeviceFeature>();

		private static BatteryState BuildBatteryState(BatteryPowerState batteryPowerState)
			=> new()
			{
				Level = batteryPowerState.BatteryLevel / 100f,
				BatteryStatus = batteryPowerState.ChargeStatus switch
				{
					BatteryChargeStatus.Discharging => BatteryStatus.Discharging,
					BatteryChargeStatus.Charging => BatteryStatus.Charging,
					BatteryChargeStatus.ChargingNearlyComplete => BatteryStatus.ChargingNearlyComplete,
					BatteryChargeStatus.ChargingComplete => BatteryStatus.ChargingComplete,
					BatteryChargeStatus.ChargingError => BatteryStatus.Error,
					BatteryChargeStatus.InvalidBatteryType => BatteryStatus.Invalid,
					BatteryChargeStatus.BatteryTooHot => BatteryStatus.TooHot,
					_ => BatteryStatus.Error,
				},
				ExternalPowerStatus =
					((batteryPowerState.ExternalPowerStatus & BatteryExternalPowerStatus.IsConnected) != 0 ? ExternalPowerStatus.IsConnected : 0) |
					((batteryPowerState.ExternalPowerStatus & BatteryExternalPowerStatus.IsChargingBelowOptimalSpeed) != 0 ? ExternalPowerStatus.IsSlowCharger : 0)
			};

		private static BacklightState BuildBacklightState(DeviceTools.Logitech.HidPlusPlus.BacklightState backlightState)
			=> new()
			{
				CurrentLevel = backlightState.CurrentLevel,
				MaximumLevel = (byte)(backlightState.LevelCount - 1),
			};

		private void OnBatteryChargeStateChanged(HidPlusPlusDevice.FeatureAccess device, BatteryPowerState batteryPowerState)
		{
			if (BatteryStateChanged is { } batteryStateChanged)
			{
				_ = Task.Run
				(
					() =>
					{
						try
						{
							batteryStateChanged.Invoke(this, BuildBatteryState(batteryPowerState));
						}
						catch (Exception ex)
						{
							_logger.LogitechUniversalDriverBatteryStateChangedError(ex);
						}
					}
				);
			}
		}

		private void OnLockKeysChanged(HidPlusPlusDevice.FeatureAccess device, DeviceTools.Logitech.HidPlusPlus.LockKeys lockKeys)
		{
			if (LockKeysChanged is { } lockKeysChanged)
			{
				_ = Task.Run
				(
					() =>
					{
						try
						{
							lockKeysChanged.Invoke(this, (LockKeys)(byte)lockKeys);
						}
						catch (Exception ex)
						{
							_logger.LogitechUniversalDriverLockKeysChangedError(ex);
						}
					}
				);
			}
		}

		private void OnBacklightStateChanged(HidPlusPlusDevice.FeatureAccess device, DeviceTools.Logitech.HidPlusPlus.BacklightState backlightState)
		{
			if (BacklightStateChanged is { } backlightStateChanged)
			{
				_ = Task.Run
				(
					() =>
					{
						try
						{
							backlightStateChanged.Invoke(this, BuildBacklightState(backlightState));
						}
						catch (Exception ex)
						{
							_logger.LogitechUniversalDriverBacklightStateChangedError(ex);
						}
					}
				);
			}
		}

		private void OnDpiChanged(HidPlusPlusDevice device, DpiStatus status)
		{
			if (DpiChanged is { } dpiChanged)
			{
				_ = Task.Run
				(
					() =>
					{
						try
						{
							dpiChanged.Invoke(this, Convert(status));
						}
						catch (Exception ex)
						{
							_logger.LogitechUniversalDriverBacklightStateChangedError(ex);
						}
					}
				);
			}
		}

		protected HidPlusPlusDevice.FeatureAccess Device => Unsafe.As<HidPlusPlusDevice.FeatureAccess>(_device);

		protected bool HasBattery => Device.HasBatteryInformation;
		protected bool HasBacklight => Device.HasBacklight;
		protected bool HasLockKeys => Device.HasLockKeys;
		protected bool HasAdjustableDpi => Device.HasAdjustableDpi;
		protected bool HasAdjustableReportInterval => Device.HasAdjustableReportInterval;
		protected bool HasOnBoardProfiles => Device.HasOnBoardProfiles;

		private event Action<Driver, BatteryState>? BatteryStateChanged;
		private event Action<Driver, BacklightState>? BacklightStateChanged;
		private event Action<Driver, LockKeys>? LockKeysChanged;

		event Action<Driver, BatteryState> IBatteryStateDeviceFeature.BatteryStateChanged
		{
			add => BatteryStateChanged += value;
			remove => BatteryStateChanged -= value;
		}

		BatteryState IBatteryStateDeviceFeature.BatteryState => BuildBatteryState(Device.BatteryPowerState);

		event Action<Driver, BacklightState> IKeyboardBacklightFeature.BacklightStateChanged
		{
			add => BacklightStateChanged += value;
			remove => BacklightStateChanged -= value;
		}

		BacklightState IKeyboardBacklightFeature.BacklightState => BuildBacklightState(Device.BacklightState);

		event Action<Driver, LockKeys> IKeyboardLockKeysFeature.LockedKeysChanged
		{
			add => LockKeysChanged += value;
			remove => LockKeysChanged -= value;
		}

		LockKeys IKeyboardLockKeysFeature.LockedKeys => (LockKeys)(byte)Device.LockKeys;

		ushort IMouseConfigurablePollingFrequencyFeature.PollingFrequency => (ushort)(1000 / Device.ReportInterval);

		ImmutableArray<ushort> IMouseConfigurablePollingFrequencyFeature.SupportedPollingFrequencies
		{
			get
			{
				var supportedReportIntervals = Device.SupportedReportIntervals;
				var frequencies = new ushort[BitOperations.PopCount((byte)supportedReportIntervals)];
				int i = 0;
				for (int j = 128; j != 0; j >>>= 1)
				{
					if (((byte)supportedReportIntervals & j) != 0) frequencies[i++] = (ushort)(1000 / ((uint)BitOperations.Log2((uint)j) + 1));
				}
				return ImmutableCollectionsMarshal.AsImmutableArray(frequencies);
			}
		}

		async ValueTask IMouseConfigurablePollingFrequencyFeature.SetPollingFrequencyAsync(ushort pollingFrequency, CancellationToken cancellationToken)
		{
			if (pollingFrequency is < 125 or > 1000) throw new ArgumentOutOfRangeException(nameof(pollingFrequency));
			// NB: As long as we work with integers and the allowed polling intervals, (1000 / frequency) is a perfect reverse of (1000 / interval).
			// This is however an imperfect solution, but I don't see a perfect solution to this anyway.
			// While migrating the feature to use µs intervals instead might seem like a good idea,
			// it will be problematic as soon as somebody comes up with hardware that has a 16KHz frequency. (Which is 62.5µs interval)
			byte interval = (byte)(1000 / pollingFrequency);
			await Device.SetReportIntervalAsync(interval, cancellationToken).ConfigureAwait(false);
		}

		private static MouseDpiStatus Convert(DpiStatus dpiStatus)
			=> new() { PresetIndex = dpiStatus.PresetIndex, Dpi = Convert(dpiStatus.Dpi) };

		private static DotsPerInch Convert(DeviceTools.Logitech.HidPlusPlus.DotsPerInch dpi)
			=> new(dpi.Horizontal, dpi.Vertical);

		MouseDpiStatus IMouseDpiFeature.CurrentDpi => Convert(Device.CurrentDpi);

		DotsPerInch IMouseDynamicDpiFeature.MaximumDpi => new(Device.DpiRanges[^1].Maximum);

		bool IMouseDynamicDpiFeature.AllowsSeparateXYDpi => false;

		private event Action<Driver, MouseDpiStatus> DpiChanged;

		event Action<Driver, MouseDpiStatus> IMouseDynamicDpiFeature.DpiChanged
		{
			add => DpiChanged += value;
			remove => DpiChanged -= value;
		}

		ImmutableArray<DotsPerInch> IMouseDpiPresetsFeature.DpiPresets => ImmutableArray.CreateRange(Device.GetCurrentDpiPresets(), Convert);

		async ValueTask IMouseDpiPresetsFeature.ChangeCurrentPresetAsync(byte activePresetIndex, CancellationToken cancellationToken)
		{
			await Device.SetCurrentDpiPresetAsync(activePresetIndex, cancellationToken).ConfigureAwait(false);
			DpiChanged?.Invoke(this, Convert(Device.CurrentDpi));
		}

		byte IMouseConfigurableDpiPresetsFeature.MaxPresetCount => 5;

		ValueTask IMouseConfigurableDpiPresetsFeature.SetDpiPresetsAsync(byte activePresetIndex, ImmutableArray<DotsPerInch> dpiPresets, CancellationToken cancellationToken) => throw new NotImplementedException();
	}

	private abstract class FeatureAccessDirect : FeatureAccess
	{
		public FeatureAccessDirect(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirect> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
			: base(device, logger, configurationKey, versionNumber)
		{
		}
	}

	private abstract class FeatureAccessThroughReceiver : FeatureAccess
	{
		public FeatureAccessThroughReceiver(HidPlusPlusDevice.FeatureAccessThroughReceiver device, ILogger<FeatureAccessThroughReceiver> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
			: base(device, logger, configurationKey, versionNumber)
		{
		}
	}
}
