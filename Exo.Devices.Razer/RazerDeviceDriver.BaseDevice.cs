using System.Collections.Immutable;
using DeviceTools;
using Exo.Devices.Razer.LightingEffects;
using Exo.Features;
using Exo.Features.Lighting;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Razer;

public abstract partial class RazerDeviceDriver
{
	private abstract class BaseDevice :
		RazerDeviceDriver,
		IDeviceDriver<ILightingDeviceFeature>,
		IBatteryStateDeviceFeature
	{
		private abstract class LightingZone :
			ILightingZone,
			ILightingZoneEffect<DisabledEffect>,
			ILightingDeferredChangesFeature,
			IPersistentLightingFeature,
			ILightingBrightnessFeature
		{
			protected BaseDevice Device { get; }

			public Guid ZoneId { get; }

			public LightingZone(BaseDevice device, Guid zoneId)
			{
				Device = device;
				ZoneId = zoneId;
			}

			public ILightingEffect GetCurrentEffect() => Device._currentEffect;

			void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect) => Device.SetCurrentEffect(DisabledEffect.SharedInstance);
			bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => Device._currentEffect.TryGetEffect(out effect);

			ValueTask ILightingDeferredChangesFeature.ApplyChangesAsync() => Device.ApplyChangesAsync(false, default);
			ValueTask IPersistentLightingFeature.PersistCurrentConfigurationAsync() => Device.ApplyChangesAsync(true, default);

			byte ILightingBrightnessFeature.MaximumBrightness => 255;
			byte ILightingBrightnessFeature.CurrentBrightness
			{
				get => Device.CurrentBrightness;
				set => Device.CurrentBrightness = value;
			}
		}

		private class BasicLightingZone : LightingZone,
			ILightingZoneEffect<StaticColorEffect>,
			ILightingZoneEffect<ColorPulseEffect>,
			ILightingZoneEffect<TwoColorPulseEffect>,
			ILightingZoneEffect<RandomColorPulseEffect>,
			ILightingZoneEffect<SpectrumCycleEffect>,
			ILightingZoneEffect<SpectrumWaveEffect>
		{
			public BasicLightingZone(BaseDevice device, Guid zoneId) : base(device, zoneId)
			{
			}

			void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect) => Device.SetCurrentEffect(effect);
			void ILightingZoneEffect<ColorPulseEffect>.ApplyEffect(in ColorPulseEffect effect) => Device.SetCurrentEffect(effect);
			void ILightingZoneEffect<TwoColorPulseEffect>.ApplyEffect(in TwoColorPulseEffect effect) => Device.SetCurrentEffect(effect);
			void ILightingZoneEffect<RandomColorPulseEffect>.ApplyEffect(in RandomColorPulseEffect effect) => Device.SetCurrentEffect(RandomColorPulseEffect.SharedInstance);
			void ILightingZoneEffect<SpectrumCycleEffect>.ApplyEffect(in SpectrumCycleEffect effect) => Device.SetCurrentEffect(SpectrumCycleEffect.SharedInstance);
			void ILightingZoneEffect<SpectrumWaveEffect>.ApplyEffect(in SpectrumWaveEffect effect) => Device.SetCurrentEffect(SpectrumWaveEffect.SharedInstance);

			bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => Device._currentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<ColorPulseEffect>.TryGetCurrentEffect(out ColorPulseEffect effect) => Device._currentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<TwoColorPulseEffect>.TryGetCurrentEffect(out TwoColorPulseEffect effect) => Device._currentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<RandomColorPulseEffect>.TryGetCurrentEffect(out RandomColorPulseEffect effect) => Device._currentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<SpectrumCycleEffect>.TryGetCurrentEffect(out SpectrumCycleEffect effect) => Device._currentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<SpectrumWaveEffect>.TryGetCurrentEffect(out SpectrumWaveEffect effect) => Device._currentEffect.TryGetEffect(out effect);
		}

		private class ReactiveLightingZone : BasicLightingZone, ILightingZoneEffect<ReactiveEffect>
		{
			public ReactiveLightingZone(BaseDevice device, Guid zoneId) : base(device, zoneId)
			{
			}

			void ILightingZoneEffect<ReactiveEffect>.ApplyEffect(in ReactiveEffect effect) => Device.SetCurrentEffect(effect);
			bool ILightingZoneEffect<ReactiveEffect>.TryGetCurrentEffect(out ReactiveEffect effect) => Device._currentEffect.TryGetEffect(out effect);
		}

		private class UnifiedBasicLightingZone : BasicLightingZone, IUnifiedLightingFeature
		{
			public bool IsUnifiedLightingEnabled => true;

			public UnifiedBasicLightingZone(BaseDevice device, Guid zoneId) : base(device, zoneId)
			{
			}
		}

		private class UnifiedReactiveLightingZone : ReactiveLightingZone, IUnifiedLightingFeature
		{
			public bool IsUnifiedLightingEnabled => true;

			public UnifiedReactiveLightingZone(BaseDevice device, Guid zoneId) : base(device, zoneId)
			{
			}
		}

		private ILightingEffect _appliedEffect;
		private ILightingEffect _currentEffect;
		private byte _appliedBrightness;
		private byte _currentBrightness;
		private readonly AsyncLock _lightingLock;
		private readonly AsyncLock _batteryStateLock;
		private readonly IDeviceFeatureSet<ILightingDeviceFeature> _lightingFeatures;
		// How do we use this ?
		private readonly byte _deviceIndex;
		private ushort _batteryLevelAndChargingStatus;

		private bool HasBattery => (_deviceFlags & RazerDeviceFlags.HasBattery) != 0;
		private bool HasReactiveLighting => (_deviceFlags & RazerDeviceFlags.HasReactiveLighting) != 0;
		private bool IsWired => _deviceIdSource == DeviceIdSource.Usb;

		protected BaseDevice
		(
			IRazerProtocolTransport transport,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			ImmutableArray<DeviceId> deviceIds,
			byte mainDeviceIdIndex,
			RazerDeviceFlags deviceFlags
		) : base(transport, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
		{
			_appliedEffect = DisabledEffect.SharedInstance;
			_currentEffect = DisabledEffect.SharedInstance;
			_lightingLock = new();
			_batteryStateLock = new();
			_currentBrightness = 0x54; // 33%

			_lightingFeatures = HasReactiveLighting ?
				FeatureSet.Create<
					ILightingDeviceFeature,
					UnifiedReactiveLightingZone,
					ILightingDeferredChangesFeature,
					IPersistentLightingFeature,
					IUnifiedLightingFeature,
					ILightingBrightnessFeature>(new(this, lightingZoneId)) :
				FeatureSet.Create<ILightingDeviceFeature,
					UnifiedBasicLightingZone,
					ILightingDeferredChangesFeature,
					IPersistentLightingFeature,
					IUnifiedLightingFeature,
					ILightingBrightnessFeature>(new(this, lightingZoneId));
		}

		protected override async ValueTask InitializeAsync(CancellationToken cancellationToken)
		{
			await base.InitializeAsync(cancellationToken).ConfigureAwait(false);
			if (HasBattery)
			{
				ApplyBatteryLevelAndChargeStatusUpdate
				(
					3,
					await _transport.GetBatteryLevelAsync(cancellationToken).ConfigureAwait(false),
					await _transport.IsConnectedToExternalPowerAsync(cancellationToken).ConfigureAwait(false)
				);
			}

			// No idea if that's the right thing to do but it seem to produce some valid good results. (Might just be by coincidence)
			byte flag = await _transport.GetDeviceInformationXxxxxAsync(cancellationToken).ConfigureAwait(false);
			_appliedEffect = await _transport.GetSavedEffectAsync(flag, cancellationToken).ConfigureAwait(false) ?? DisabledEffect.SharedInstance;

			// Reapply the persisted effect. (In case it was overridden by a temporary effect)
			await ApplyEffectAsync(_appliedEffect, _currentBrightness, false, true, cancellationToken).ConfigureAwait(false);

			_currentEffect = _appliedEffect;
		}

		protected override IDeviceFeatureSet<IGenericDeviceFeature> CreateGenericFeatures()
			=> HasSerialNumber ?
				HasBattery ?
					FeatureSet.Create<IGenericDeviceFeature, BaseDevice, IDeviceIdFeature, IDeviceSerialNumberFeature, IBatteryStateDeviceFeature>(this) :
					FeatureSet.Create<IGenericDeviceFeature, BaseDevice, IDeviceIdFeature, IDeviceSerialNumberFeature>(this) :
				HasBattery ?
					FeatureSet.Create<IGenericDeviceFeature, BaseDevice, IDeviceIdFeature, IBatteryStateDeviceFeature>(this) :
					FeatureSet.Create<IGenericDeviceFeature, BaseDevice, IDeviceIdFeature>(this);

		protected override void OnDeviceExternalPowerChange(bool isCharging)
			=> ApplyBatteryLevelAndChargeStatusUpdate(2, 0, isCharging);

		protected override void OnDeviceBatteryLevelChange(byte batteryLevel)
			=> ApplyBatteryLevelAndChargeStatusUpdate(1, batteryLevel, false);

		private void ApplyBatteryLevelAndChargeStatusUpdate(byte changeType, byte newBatteryLevel, bool isCharging)
		{
			if (changeType == 0) return;

			lock (_batteryStateLock)
			{
				ushort oldBatteryLevelAndChargingStatus = _batteryLevelAndChargingStatus;
				ushort newBatteryLevelAndChargingStatus = 0;

				if ((changeType & 1) != 0)
				{
					newBatteryLevelAndChargingStatus = newBatteryLevel;
				}
				else
				{
					newBatteryLevelAndChargingStatus = (byte)oldBatteryLevelAndChargingStatus;
				}

				if ((changeType & 2) != 0)
				{
					newBatteryLevelAndChargingStatus = (ushort)(newBatteryLevelAndChargingStatus | (isCharging ? 0x100 : 0));
				}
				else
				{
					newBatteryLevelAndChargingStatus = (ushort)(newBatteryLevelAndChargingStatus | (oldBatteryLevelAndChargingStatus & 0xFF00));
				}

				if (oldBatteryLevelAndChargingStatus != newBatteryLevelAndChargingStatus)
				{
					Volatile.Write(ref _batteryLevelAndChargingStatus, newBatteryLevelAndChargingStatus);

					if (BatteryStateChanged is { } batteryStateChanged)
					{
						_ = Task.Run
						(
							() =>
							{
								try
								{
									batteryStateChanged.Invoke(this, BuildBatteryState(newBatteryLevelAndChargingStatus));
								}
								catch (Exception ex)
								{
									// TODO: Log
								}
							}
						);
					}
				}
			}
		}

		protected IDeviceFeatureSet<ILightingDeviceFeature> LightingFeatures => _lightingFeatures;
		IDeviceFeatureSet<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;

		private byte CurrentBrightness
		{
			get => Volatile.Read(ref _currentBrightness);
			set
			{
				lock (_lightingLock)
				{
					_currentBrightness = value;
				}
			}
		}

		private async ValueTask ApplyChangesAsync(bool shouldPersist, CancellationToken cancellationToken)
		{
			using (await _lightingLock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (shouldPersist || !ReferenceEquals(_appliedEffect, _currentEffect))
				{
					await ApplyEffectAsync(_currentEffect, _currentBrightness, shouldPersist, _appliedEffect is DisabledEffect || _appliedBrightness != _currentBrightness, cancellationToken).ConfigureAwait(false);
					_appliedEffect = _currentEffect;
				}
				else if (!ReferenceEquals(_currentEffect, DisabledEffect.SharedInstance) && _appliedBrightness != _currentBrightness)
				{
					await _transport.SetBrightnessAsync(shouldPersist, _currentBrightness, cancellationToken).ConfigureAwait(false);
				}
				_appliedBrightness = _currentBrightness;
			}
		}

		private async ValueTask ApplyEffectAsync(ILightingEffect effect, byte brightness, bool shouldPersist, bool forceBrightnessUpdate, CancellationToken cancellationToken)
		{
			if (ReferenceEquals(effect, DisabledEffect.SharedInstance))
			{
				await _transport.SetEffectAsync(shouldPersist, 0, 0, default, default, cancellationToken).ConfigureAwait(false);
				await _transport.SetBrightnessAsync(shouldPersist, 0, cancellationToken);
				return;
			}

			// It seems brightness must be restored from zero first before setting a color effect.
			// Otherwise, the device might restore to its saved effect. (e.g. Color Cycle)
			if (forceBrightnessUpdate)
			{
				await _transport.SetBrightnessAsync(shouldPersist, brightness, cancellationToken);
			}

			switch (effect)
			{
			case StaticColorEffect staticColorEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.Static, 1, staticColorEffect.Color, staticColorEffect.Color, cancellationToken);
				break;
			case RandomColorPulseEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.Breathing, 0, default, default, cancellationToken);
				break;
			case ColorPulseEffect colorPulseEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.Breathing, 1, colorPulseEffect.Color, default, cancellationToken);
				break;
			case TwoColorPulseEffect twoColorPulseEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.Breathing, 2, twoColorPulseEffect.Color, twoColorPulseEffect.SecondColor, cancellationToken);
				break;
			case SpectrumCycleEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.SpectrumCycle, 0, default, default, cancellationToken);
				break;
			case SpectrumWaveEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.Wave, 0, default, default, cancellationToken);
				break;
			case ReactiveEffect reactiveEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.Reactive, 1, reactiveEffect.Color, default, cancellationToken);
				break;
			}
		}

		private void SetCurrentEffect(ILightingEffect effect)
		{
			lock (_lightingLock)
			{
				_currentEffect = effect;
			}
		}

		// TODO: Determine how this should be exposed.
		public void SetDefaultBrightness(byte brightness)
		{
			_currentBrightness = brightness;
		}

		private event Action<Driver, BatteryState>? BatteryStateChanged;

		event Action<Driver, BatteryState> IBatteryStateDeviceFeature.BatteryStateChanged
		{
			add => BatteryStateChanged += value;
			remove => BatteryStateChanged -= value;
		}

		BatteryState IBatteryStateDeviceFeature.BatteryState
			=> BuildBatteryState(Volatile.Read(ref _batteryLevelAndChargingStatus));

		private BatteryState BuildBatteryState(ushort rawBatteryLevelAndChargingStatus)
		{
			// Reasoning:
			// If the device is directly connected to USB, it is not in wireless mode.
			// If the device is wireless, it is discharging. Otherwise, it is charging up to 100%.
			// This is based on the current state of things. It could change depending on the technical possibilities.
			bool isWired = IsWired;

			bool isCharging = (rawBatteryLevelAndChargingStatus & 0x100) != 0;
			byte batteryLevel = (byte)rawBatteryLevelAndChargingStatus;

			return new()
			{
				Level = batteryLevel / 255f,
				BatteryStatus = isWired || isCharging ?
					batteryLevel == 255 ? BatteryStatus.ChargingComplete : BatteryStatus.Charging :
					BatteryStatus.Discharging,
				// NB: What meaning should we put behind external power ? If the mouse is on a dock it technically has external power, but it is not usable…
				ExternalPowerStatus = isWired || isCharging ? ExternalPowerStatus.IsConnected : ExternalPowerStatus.IsDisconnected,
			};
		}
	}
}
