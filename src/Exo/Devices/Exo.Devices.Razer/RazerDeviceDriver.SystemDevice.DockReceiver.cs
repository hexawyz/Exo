using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using DeviceTools;
using Exo.Devices.Razer.LightingEffects;
using Exo.Features.Lighting;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Razer;

public abstract partial class RazerDeviceDriver
{
	private static partial class SystemDevice
	{
		// Dock receivers are acting simultaneously as receivers and as a dock for a single device, unlike newer USB receivers that are dongle. (e.g. Razer Mamba Chroma Dock)
		// The code here will be very similar to but simpler than UsbReceiver, as these device do not support pairing with other devices than the one they came with.
		// (Well, they probably do support re-pairing with another identical device, but I don't even know if Synapse is able to do this, so we should clearly not care for now)
		// TODO: Implement the lighting for the dock. (As a technical "children" of the mouse device)
		public sealed class DockReceiver : BaseDevice
		{
			private sealed class DockLightingZone : LightingZone,
				ILightingZoneEffect<StaticColorEffect>,
				ILightingZoneEffect<RandomColorPulseEffect>,
				ILightingZoneEffect<ColorPulseEffect>,
				ILightingZoneEffect<TwoColorPulseEffect>,
				ILightingZoneEffect<SpectrumCycleEffect>,
				ILightingZoneEffect<SynchronizedEffect>,
				IUnifiedLightingFeature
			{
				private readonly RazerLedId _ledId;

				public DockLightingZone(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId)
				{
					_ledId = ledId;
				}

				protected override ValueTask<byte> ReadBrightnessAsync(byte profileId, CancellationToken cancellationToken)
					=> Transport.GetBrightnessV1Async(_ledId, cancellationToken);

				protected override Task ApplyBrightnessAsync(byte profileId, byte brightness, CancellationToken cancellationToken)
					=> Transport.SetBrightnessV1Async(_ledId, brightness, cancellationToken);

				protected override async ValueTask<ILightingEffect?> ReadEffectAsync(byte profileId, CancellationToken cancellationToken)
				{
					if (await Transport.IsSynchronizedLightingEnabledV1Async(_ledId, cancellationToken).ConfigureAwait(false))
					{
						return SynchronizedEffect.SharedInstance;
					}
					else return DisabledEffect.SharedInstance;
				}

				protected override async Task ApplyEffectAsync(byte profileId, ILightingEffect effect, CancellationToken cancellationToken)
				{
					var transport = Transport;

					// TODO: Refactor to have better state management.
					// Need to entirely split out the logic from the base class, as it turns out.
					// Basically, with this feature, the effects parameters are set using different functions, which may or may not override eachother.
					// We do not need to overwrite settings that already have the correct value and as such, we can save I/O.
					bool shouldEnableLed = true;
					bool shouldDisableSynchronization = true;
					bool shouldChangeCurrentEffect = true;

					RazerLightingEffectV1 effectId = RazerLightingEffectV1.Disabled;

					switch (effect)
					{
					case DisabledEffect:
						await transport.EnableLedV1Async(_ledId, false, cancellationToken);
						shouldEnableLed = false;
						break;
					case StaticColorEffect staticColorEffect:
						await transport.SetStaticColorV1Async(_ledId, staticColorEffect.Color, cancellationToken);
						effectId = RazerLightingEffectV1.Static;
						break;
					case RandomColorPulseEffect:
						await transport.SetBreathingEffectParametersV1Async(_ledId, cancellationToken);
						effectId = RazerLightingEffectV1.Breathing;
						break;
					case ColorPulseEffect colorPulseEffect:
						await transport.SetBreathingEffectParametersV1Async(_ledId, colorPulseEffect.Color, cancellationToken);
						effectId = RazerLightingEffectV1.Breathing;
						break;
					case TwoColorPulseEffect twoColorPulseEffect:
						await transport.SetBreathingEffectParametersV1Async(_ledId, twoColorPulseEffect.Color, twoColorPulseEffect.SecondColor, cancellationToken);
						effectId = RazerLightingEffectV1.Breathing;
						break;
					case SpectrumCycleEffect:
						effectId = RazerLightingEffectV1.SpectrumCycle;
						break;
					case SynchronizedEffect:
						await transport.SetSynchronizedLightingV1Async(_ledId, true, cancellationToken);
						shouldEnableLed = false;
						shouldDisableSynchronization = false;
						shouldChangeCurrentEffect = false;
						break;
					default:
						throw ExceptionDispatchInfo.SetCurrentStackTrace(new ArgumentException("Unsupported effect"));
					}

					if (shouldChangeCurrentEffect)
					{
						await transport.SetEffectV1Async(_ledId, effectId, cancellationToken);
					}
					if (shouldEnableLed)
					{
						await transport.EnableLedV1Async(_ledId, true, cancellationToken);
					}
					if (shouldDisableSynchronization)
					{
						await transport.SetSynchronizedLightingV1Async(_ledId, false, cancellationToken).ConfigureAwait(false);
					}
				}

				void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect) => SetCurrentEffect(effect);
				bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => CurrentEffect.TryGetEffect(out effect);

				void ILightingZoneEffect<RandomColorPulseEffect>.ApplyEffect(in RandomColorPulseEffect effect) => SetCurrentEffect(effect);
				bool ILightingZoneEffect<RandomColorPulseEffect>.TryGetCurrentEffect(out RandomColorPulseEffect effect) => CurrentEffect.TryGetEffect(out effect);

				void ILightingZoneEffect<ColorPulseEffect>.ApplyEffect(in ColorPulseEffect effect) => SetCurrentEffect(effect);
				bool ILightingZoneEffect<ColorPulseEffect>.TryGetCurrentEffect(out ColorPulseEffect effect) => CurrentEffect.TryGetEffect(out effect);

				void ILightingZoneEffect<TwoColorPulseEffect>.ApplyEffect(in TwoColorPulseEffect effect) => SetCurrentEffect(effect);
				bool ILightingZoneEffect<TwoColorPulseEffect>.TryGetCurrentEffect(out TwoColorPulseEffect effect) => CurrentEffect.TryGetEffect(out effect);

				void ILightingZoneEffect<SpectrumCycleEffect>.ApplyEffect(in SpectrumCycleEffect effect) => SetCurrentEffect(effect);
				bool ILightingZoneEffect<SpectrumCycleEffect>.TryGetCurrentEffect(out SpectrumCycleEffect effect) => CurrentEffect.TryGetEffect(out effect);

				void ILightingZoneEffect<SynchronizedEffect>.ApplyEffect(in SynchronizedEffect effect) => SetCurrentEffect(effect);
				bool ILightingZoneEffect<SynchronizedEffect>.TryGetCurrentEffect(out SynchronizedEffect effect) => CurrentEffect.TryGetEffect(out effect);
			}

			private readonly RazerDeviceNotificationWatcher _watcher;
			private readonly IDriverRegistry _driverRegistry;
			private RazerDeviceDriver? _pairedDevice;
			private readonly AsyncLock _childDeviceLock;

			public DockReceiver
			(
				IRazerProtocolTransport transport,
				DeviceStream notificationStream,
				DeviceNotificationOptions deviceNotificationOptions,
				IDriverRegistry driverRegistry,
				in DeviceInformation deviceInformation,
				ImmutableArray<RazerLedId> ledIds,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex,
				RazerDeviceFlags deviceFlags
			) : base(transport, deviceInformation, ledIds, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
			{
				_driverRegistry = driverRegistry;
				_childDeviceLock = new();
				_watcher = new(notificationStream, this, deviceNotificationOptions);
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.MouseDock;

			protected override (LightingZone? UnifiedLightingZone, ImmutableArray<LightingZone> LightingZones) CreateLightingZones(in DeviceInformation deviceInformation, ImmutableArray<RazerLedId> ledIds)
				=> (new DockLightingZone(this, deviceInformation.LightingZoneGuid.GetValueOrDefault(), RazerLedId.Dongle), []);

			protected override async ValueTask InitializeAsync(CancellationToken cancellationToken)
			{
				await base.InitializeAsync(cancellationToken);

				using (await _childDeviceLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					// Do a fake battery level reading to detect if the dock has been initialized.
					// These "dumb" dock devices will cache all responses from the mouse after it becomes online the first time, but will return an error before that.
					// The mouse will take lighting decisions for the dock, but the dock can buffer lighting changes for when the mouse comes back up.
					try
					{
						var r = await _transport.GetBatteryLevelAsync(cancellationToken).ConfigureAwait(false);
					}
					catch
					{
						return;
					}

					// If the above call succeeded, we proceed on to create the device.
					await HandleDeviceArrivalAsync(cancellationToken).ConfigureAwait(false);
				}
			}

			protected override void OnDeviceAvailabilityChange(byte notificationStreamIndex) => HandleChildDeviceStateChange();

			private void HandleChildDeviceStateChange()
				=> _ = HandleChildDeviceStateChangeAsync(default);

			private async ValueTask HandleChildDeviceStateChangeAsync(CancellationToken cancellationToken)
			{
				try
				{
					using (await _childDeviceLock.WaitAsync(cancellationToken).ConfigureAwait(false))
					{
						// NB: Maybe some newer dock devices support proper device offline notifications, so I left that path in the code. However, the Mamba Chroma will never appear offline once initialized.
						// TODO: Investigate more about a wait to test for device availability.
						try
						{
							var r = await _transport.GetBatteryLevelAsync(cancellationToken).ConfigureAwait(false);
						}
						catch
						{
							// If the call failed, we interpret that as the device being offline.
							await HandleDeviceRemovalAsync(cancellationToken).ConfigureAwait(false);
							return;
						}

						await HandleDeviceArrivalAsync(cancellationToken).ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
				}
			}

			private async Task HandleDeviceArrivalAsync(CancellationToken cancellationToken)
			{
				// Don't recreate a driver if one is already present.
				if (Volatile.Read(ref _pairedDevice) is not null) return;

				var deviceInformation = GetDeviceInformation(_deviceIds[_mainDeviceIdIndex].ProductId);

				// TODO: Log unsupported device.
				if (Unsafe.IsNullRef(in deviceInformation)) return;

				// We use a similar logic as for other USB receivers here, however we could avoid doing the receiver info lookup because the device ID is always known in advance.
				if (deviceInformation.IsReceiver)
				{
					deviceInformation = GetDeviceInformation(deviceInformation.WiredDeviceProductId);

					if (Unsafe.IsNullRef(in deviceInformation)) return;
				}

				RazerDeviceDriver driver;

				try
				{
					var serialNumber = await _transport.GetSerialNumberAsync(default).ConfigureAwait(false);

					driver = await CreateChildDeviceAsync
					(
						_transport,
						DeviceIdSource.Unknown,
						0xFFFF,
						0,
						deviceInformation,
						deviceInformation.FriendlyName,
						ConfigurationKey.DeviceMainId,
						serialNumber,
						cancellationToken
					).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					// TODO: Log exception

					return;
				}

				try
				{
					await _driverRegistry.AddDriverAsync(driver).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					// TODO: Log exception

					await driver.DisposeAsync().ConfigureAwait(false);
					return;
				}

				if (Interlocked.Exchange(ref _pairedDevice, driver) is { } oldDriver)
				{
					// TODO: Log an error. We should never have to replace a live driver by another.

					await RemoveAndDisposeDriverAsync(oldDriver).ConfigureAwait(false);
				}
			}

			private async Task HandleDeviceRemovalAsync(CancellationToken cancellationToken)
			{
				if (Interlocked.Exchange(ref _pairedDevice, null) is { } oldDriver)
				{
					await RemoveAndDisposeDriverAsync(oldDriver).ConfigureAwait(false);
				}
			}

			private async Task RemoveAndDisposeDriverAsync(RazerDeviceDriver driver)
			{
				try
				{
					await _driverRegistry.RemoveDriverAsync(driver).ConfigureAwait(false);
					await driver.DisposeAsync().ConfigureAwait(false);
				}
				catch
				{
					// TODO: Log
				}
			}
		}
	}
}
