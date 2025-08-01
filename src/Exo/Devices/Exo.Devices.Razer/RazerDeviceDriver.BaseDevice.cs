using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using DeviceTools;
using Exo.Devices.Razer.LightingEffects;
using Exo.Features;
using Exo.Features.Lighting;
using Exo.Features.PowerManagement;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Razer;

public abstract partial class RazerDeviceDriver
{
	private abstract class BaseDevice :
		RazerDeviceDriver,
		IDeviceDriver<IPowerManagementDeviceFeature>,
		IDeviceDriver<ILightingDeviceFeature>,
		ILightingControllerFeature,
		ILightingPersistenceFeature,
		ILightingDeferredChangesFeature,
		ILightingBrightnessFeature,
		IBatteryStateDeviceFeature,
		IIdleSleepTimerFeature,
		ILowPowerModeBatteryThresholdFeature,
		IWirelessBrightnessFeature
	{
		protected abstract class LightingZone :
			ILightingZone,
			ILightingZoneEffect<DisabledEffect>
		{
			private readonly BaseDevice _device;
			private ILightingEffect _appliedEffect;
			private ILightingEffect _currentEffect;
			private byte _appliedBrightness;
			private byte _currentBrightness;
			private readonly Guid _zoneId;

			protected BaseDevice Device => _device;
			protected IRazerProtocolTransport Transport => Device._transport;
			public Guid ZoneId => _zoneId;
			private bool IsUnifiedLightingZone => ReferenceEquals(this, _device._unifiedLightingZone);
			protected ILightingEffect CurrentEffect => Device._isUnifiedLightingEnabled == IsUnifiedLightingZone ? _currentEffect : NotApplicableEffect.SharedInstance;

			public bool IsUnifiedLightingEnabled => Device._isUnifiedLightingEnabled;

			public LightingZone(BaseDevice device, Guid zoneId)
			{
				_device = device;
				_appliedEffect = DisabledEffect.SharedInstance;
				_currentEffect = DisabledEffect.SharedInstance;
				_zoneId = zoneId;
			}

			internal async Task InitializeAsync(byte profileId, CancellationToken cancellationToken)
			{
				var effect = await ReadEffectAsync(profileId, cancellationToken).ConfigureAwait(false);
				byte brightness = await ReadBrightnessAsync(profileId, cancellationToken).ConfigureAwait(false);
				_currentEffect = _appliedEffect = effect ?? DisabledEffect.SharedInstance;
				_currentBrightness = _appliedBrightness = brightness;
				// Force reset the current effect to the saved effect.
				// Probably need to be careful there if we someday add support for some devices that only support persisted effects.
				await ApplyAsync(profileId, cancellationToken).ConfigureAwait(false);
			}

			protected abstract ValueTask<ILightingEffect?> ReadEffectAsync(byte profileId, CancellationToken cancellationToken);
			protected abstract ValueTask<byte> ReadBrightnessAsync(byte profileId, CancellationToken cancellationToken);

			internal async Task ApplyAsync(byte profileId, CancellationToken cancellationToken)
			{
				// The code below implement the common logic that we should not need to override, based on the other blocks, that we will want to implement differently for different devices.
				// This is especially important, as there are multiple different ways to set lighting effects depending on which feature and feature version the device supports.
				// e.g. Lighting initially used category `03`, but the newest ones use category `0F`. (Likely in part because it replaces the persistence flag by a profile ID)
				if (profileId != 0 || !ReferenceEquals(_appliedEffect, _currentEffect))
				{
					if (profileId != 0 || _appliedEffect is DisabledEffect || _appliedBrightness != _currentBrightness)
					{
						await ApplyBrightnessAsync(profileId, _currentBrightness, cancellationToken).ConfigureAwait(false);
						_appliedBrightness = _currentBrightness;
					}
					await ApplyEffectAsync(profileId, _currentEffect, cancellationToken).ConfigureAwait(false);
					_appliedEffect = _currentEffect;
					if (_appliedEffect is DisabledEffect && (_appliedBrightness != 0 || profileId != 0))
					{
						await ApplyBrightnessAsync(profileId, 0, cancellationToken).ConfigureAwait(false);
					}
				}
				else if (!ReferenceEquals(_currentEffect, DisabledEffect.SharedInstance) && _appliedBrightness != _currentBrightness)
				{
					await ApplyBrightnessAsync(profileId, _currentBrightness, cancellationToken).ConfigureAwait(false);
					_appliedBrightness = _currentBrightness;
				}
			}

			protected abstract Task ApplyBrightnessAsync(byte profileId, byte brightness, CancellationToken cancellationToken);

			protected abstract Task ApplyEffectAsync(byte profileId, ILightingEffect effect, CancellationToken cancellationToken);

			public ILightingEffect GetCurrentEffect() => CurrentEffect;

			// NB: We don't have a way to do an asynchronous lock here, and locking is likely not needed anyway as the effect write is atomic.
			protected void SetCurrentEffect(ILightingEffect effect)
			{
				_currentEffect = effect;
				Volatile.Write(ref Device._isUnifiedLightingEnabled, IsUnifiedLightingZone);
			}

			internal byte GetBrightness() => _currentBrightness;

			internal void SetBrightness(byte brightness)
			{
				_currentBrightness = brightness;
			}

			void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect) => SetCurrentEffect(DisabledEffect.SharedInstance);
			bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => CurrentEffect.TryGetEffect(out effect);
		}

		protected class LightingZoneV1 : LightingZone,
			ILightingZoneEffect<StaticColorEffect>,
			ILightingZoneEffect<ColorBlinkEffect>,
			ILightingZoneEffect<MultiColorBreathingEffect>,
			ILightingZoneEffect<SpectrumCycleEffect>,
			IUnifiedLightingFeature
		{
			private readonly RazerLedId _ledId;

			public LightingZoneV1(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId)
			{
				_ledId = ledId;
			}

			protected override ValueTask<byte> ReadBrightnessAsync(byte profileId, CancellationToken cancellationToken)
				=> Transport.GetBrightnessV1Async(_ledId, cancellationToken);

			protected override Task ApplyBrightnessAsync(byte profileId, byte brightness, CancellationToken cancellationToken)
				=> Transport.SetBrightnessV1Async(_ledId, brightness, cancellationToken);

			protected override async ValueTask<ILightingEffect?> ReadEffectAsync(byte profileId, CancellationToken cancellationToken)
			{
				if (this is ILightingZoneEffect<SynchronizedEffect> && await Transport.IsSynchronizedLightingEnabledV1Async(_ledId, cancellationToken).ConfigureAwait(false))
				{
					return SynchronizedEffect.SharedInstance;
				}
				if (!await Transport.IsLedEnabledV1Async(_ledId, cancellationToken).ConfigureAwait(false)) return DisabledEffect.SharedInstance;

				switch (await Transport.GetEffectV1Async(_ledId, cancellationToken).ConfigureAwait(false))
				{
				case RazerLightingEffectV0.Static:
					return new StaticColorEffect(await Transport.GetStaticColorV1Async(_ledId, cancellationToken).ConfigureAwait(false));
				case RazerLightingEffectV0.Blink:
					return new ColorBlinkEffect(await Transport.GetStaticColorV1Async(_ledId, cancellationToken).ConfigureAwait(false));
				case RazerLightingEffectV0.Breathing:
					return new MultiColorBreathingEffect([await Transport.GetStaticColorV1Async(_ledId, cancellationToken).ConfigureAwait(false)]);
				case RazerLightingEffectV0.BreathingExtended:
					try
					{
						var (colorCount, color1, color2) = await Transport.GetBreathingEffectParametersV1Async(_ledId, cancellationToken).ConfigureAwait(false);
						if (colorCount == 2) return new MultiColorBreathingEffect([color1, color2]);
						else if (colorCount == 1) return new MultiColorBreathingEffect([color1]);
						else return DisabledEffect.SharedInstance;
					}
					catch
					{
						// It might not always be possible read back this parameter for some reason?? (Or I made a mistake in the read method maybe?)
						return DisabledEffect.SharedInstance;
					}
				case RazerLightingEffectV0.SpectrumCycle:
					return SpectrumCycleEffect.SharedInstance;
				}

				// This is a lie, but default to reporting the disabled effect when read an effect that we don't support.
				return DisabledEffect.SharedInstance;
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

				RazerLightingEffectV0 effectId = RazerLightingEffectV0.Static;

				switch (effect)
				{
				case DisabledEffect:
					await transport.EnableLedV1Async(_ledId, false, cancellationToken);
					shouldEnableLed = false;
					break;
				case StaticColorEffect staticColorEffect:
					await transport.SetStaticColorV1Async(_ledId, staticColorEffect.Color, cancellationToken);
					effectId = RazerLightingEffectV0.Static;
					break;
				case ColorBlinkEffect blinkEffect:
					await transport.SetStaticColorV1Async(_ledId, blinkEffect.Color, cancellationToken);
					effectId = RazerLightingEffectV0.Blink;
					break;
				case MultiColorBreathingEffect colorBreathingEffect:
					var colors = colorBreathingEffect.Colors;
					switch (colors.Count)
					{
					case 2:
						await transport.SetBreathingEffectParametersV1Async(_ledId, colors[0], colors[1], cancellationToken);
						effectId = RazerLightingEffectV0.BreathingExtended;
						break;
					case 1:
						await transport.SetBreathingEffectParametersV1Async(_ledId, colors[0], cancellationToken);
						await transport.SetStaticColorV1Async(_ledId, colors[0], cancellationToken);
						effectId = RazerLightingEffectV0.Breathing;
						break;
					default: throw new UnreachableException();
					}
					break;
				case SpectrumCycleEffect:
					effectId = RazerLightingEffectV0.SpectrumCycle;
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

			void ILightingZoneEffect<ColorBlinkEffect>.ApplyEffect(in ColorBlinkEffect effect) => SetCurrentEffect(effect);
			bool ILightingZoneEffect<ColorBlinkEffect>.TryGetCurrentEffect(out ColorBlinkEffect effect) => CurrentEffect.TryGetEffect(out effect);

			void ILightingZoneEffect<MultiColorBreathingEffect>.ApplyEffect(in MultiColorBreathingEffect effect)
			{
				if (effect.Colors.Count == 0) throw new ArgumentException(null, nameof(effect));
				SetCurrentEffect(effect);
			}

			bool ILightingZoneEffect<MultiColorBreathingEffect>.TryGetCurrentEffect(out MultiColorBreathingEffect effect) => CurrentEffect.TryGetEffect(out effect);

			void ILightingZoneEffect<SpectrumCycleEffect>.ApplyEffect(in SpectrumCycleEffect effect) => SetCurrentEffect(effect);
			bool ILightingZoneEffect<SpectrumCycleEffect>.TryGetCurrentEffect(out SpectrumCycleEffect effect) => CurrentEffect.TryGetEffect(out effect);
		}

		protected sealed class DockLightingZoneV1 : LightingZoneV1, ILightingZoneEffect<SynchronizedEffect>
		{
			public DockLightingZoneV1(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId, ledId)
			{
			}

			void ILightingZoneEffect<SynchronizedEffect>.ApplyEffect(in SynchronizedEffect effect) => SetCurrentEffect(effect);
			bool ILightingZoneEffect<SynchronizedEffect>.TryGetCurrentEffect(out SynchronizedEffect effect) => CurrentEffect.TryGetEffect(out effect);
		}

		protected abstract class UnifiedLightingZoneV1 : LightingZone
		{
			private readonly RazerLedId _mainLedId;

			public UnifiedLightingZoneV1(BaseDevice device, Guid zoneId, RazerLedId mainLedId) : base(device, zoneId)
			{
				_mainLedId = mainLedId;
			}

			protected override ValueTask<byte> ReadBrightnessAsync(byte profileId, CancellationToken cancellationToken)
				=> Transport.GetBrightnessV1Async(_mainLedId, cancellationToken);

			protected override async ValueTask<ILightingEffect?> ReadEffectAsync(byte profileId, CancellationToken cancellationToken)
			{
				try
				{
					// Weird behavior from the Mamba Chroma: Reading does not work but in wireless mode, the request "succeed" and return the disabled effect.
					return await Transport.GetSavedEffectV1Async(cancellationToken).ConfigureAwait(false);
				}
				catch
				{
					return DisabledEffect.SharedInstance;
				}
			}

			protected override Task ApplyBrightnessAsync(byte profileId, byte brightness, CancellationToken cancellationToken)
				=> Transport.SetBrightnessV1Async(_mainLedId, brightness, cancellationToken);

			protected override async Task ApplyEffectAsync(byte profileId, ILightingEffect effect, CancellationToken cancellationToken)
			{
				var transport = Transport;

				await (effect switch
				{
					DisabledEffect staticColorEffect => transport.SetEffectV1Async(RazerLightingEffectV1.Disabled, 0, default, default, cancellationToken),
					StaticColorEffect staticColorEffect => transport.SetEffectV1Async(RazerLightingEffectV1.Static, 1, staticColorEffect.Color, staticColorEffect.Color, cancellationToken),
					RandomColorBreathingEffect => transport.SetEffectV1Async(RazerLightingEffectV1.Breathing, 3, default, default, cancellationToken),
					MultiColorBreathingEffect colorBreathingEffect => colorBreathingEffect.Colors.Count switch
					{
						2 => transport.SetEffectV1Async(RazerLightingEffectV1.Breathing, 2, colorBreathingEffect.Colors[0], colorBreathingEffect.Colors[1], cancellationToken),
						1 => transport.SetEffectV1Async(RazerLightingEffectV1.Breathing, 1, colorBreathingEffect.Colors[0], default, cancellationToken),
						_ => throw new UnreachableException()
					},
					SpectrumCycleEffect => transport.SetEffectV1Async(RazerLightingEffectV1.SpectrumCycle, 0, default, default, cancellationToken),
					SpectrumWaveEffect => transport.SetEffectV1Async(RazerLightingEffectV1.Wave, 1, default, default, cancellationToken),
					ReactiveEffect reactiveEffect => transport.SetEffectV1Async(RazerLightingEffectV1.Reactive, 1, reactiveEffect.Color, default, cancellationToken),
					_ => Task.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new ArgumentException("Unsupported effect")))
				}).ConfigureAwait(false);
			}
		}

		protected class LightingZoneV2 : LightingZone
		{
			private readonly RazerLedId _ledId;

			public LightingZoneV2(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId)
			{
				_ledId = ledId;
			}

			protected override ValueTask<byte> ReadBrightnessAsync(byte profileId, CancellationToken cancellationToken)
				=> Transport.GetBrightnessV2Async(profileId != 0, _ledId, cancellationToken);

			protected override ValueTask<ILightingEffect?> ReadEffectAsync(byte profileId, CancellationToken cancellationToken)
				=> Transport.GetSavedEffectV2Async(_ledId, cancellationToken);

			protected override Task ApplyBrightnessAsync(byte profileId, byte brightness, CancellationToken cancellationToken)
				=> Transport.SetBrightnessV2Async(profileId != 0, brightness, cancellationToken);

			protected override async Task ApplyEffectAsync(byte profileId, ILightingEffect effect, CancellationToken cancellationToken)
			{
				var transport = Transport;

				await (effect switch
				{
					DisabledEffect staticColorEffect => transport.SetEffectV2Async(profileId != 0, RazerLightingEffectV2.Disabled, 0, default, default, cancellationToken),
					StaticColorEffect staticColorEffect => transport.SetEffectV2Async(profileId != 0, RazerLightingEffectV2.Static, 1, staticColorEffect.Color, staticColorEffect.Color, cancellationToken),
					RandomColorBreathingEffect => transport.SetEffectV2Async(profileId != 0, RazerLightingEffectV2.Breathing, 0, default, default, cancellationToken),
					MultiColorBreathingEffect colorBreathingEffect => colorBreathingEffect.Colors.Count switch
					{
						2 => transport.SetEffectV2Async(profileId != 0, RazerLightingEffectV2.Breathing, 2, colorBreathingEffect.Colors[0], colorBreathingEffect.Colors[1], cancellationToken),
						1 => transport.SetEffectV2Async(profileId != 0, RazerLightingEffectV2.Breathing, 1, colorBreathingEffect.Colors[0], default, cancellationToken),
						_ => throw new UnreachableException()
					},
					SpectrumCycleEffect => transport.SetEffectV2Async(profileId != 0, RazerLightingEffectV2.SpectrumCycle, 0, default, default, cancellationToken),
					SpectrumWaveEffect => transport.SetEffectV2Async(profileId != 0, RazerLightingEffectV2.Wave, 1, default, default, cancellationToken),
					ReactiveEffect reactiveEffect => transport.SetEffectV2Async(profileId != 0, RazerLightingEffectV2.Reactive, 1, reactiveEffect.Color, default, cancellationToken),
					_ => Task.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new ArgumentException("Unsupported effect")))
				}).ConfigureAwait(false);
			}
		}

		protected class BasicLightingZoneV2 : LightingZoneV2,
			ILightingZoneEffect<StaticColorEffect>,
			ILightingZoneEffect<MultiColorBreathingEffect>,
			ILightingZoneEffect<RandomColorBreathingEffect>,
			ILightingZoneEffect<SpectrumCycleEffect>
		{
			public BasicLightingZoneV2(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId, ledId)
			{
			}

			void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect) => SetCurrentEffect(effect);

			void ILightingZoneEffect<MultiColorBreathingEffect>.ApplyEffect(in MultiColorBreathingEffect effect)
			{
				if (effect.Colors.Count == 0) throw new ArgumentException(null, nameof(effect));
				SetCurrentEffect(effect);
			}

			void ILightingZoneEffect<RandomColorBreathingEffect>.ApplyEffect(in RandomColorBreathingEffect effect) => SetCurrentEffect(RandomColorBreathingEffect.SharedInstance);
			void ILightingZoneEffect<SpectrumCycleEffect>.ApplyEffect(in SpectrumCycleEffect effect) => SetCurrentEffect(SpectrumCycleEffect.SharedInstance);

			bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => CurrentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<MultiColorBreathingEffect>.TryGetCurrentEffect(out MultiColorBreathingEffect effect) => CurrentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<RandomColorBreathingEffect>.TryGetCurrentEffect(out RandomColorBreathingEffect effect) => CurrentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<SpectrumCycleEffect>.TryGetCurrentEffect(out SpectrumCycleEffect effect) => CurrentEffect.TryGetEffect(out effect);
		}

		protected class WaveLightingZoneV2 : BasicLightingZoneV2, ILightingZoneEffect<SpectrumWaveEffect>
		{
			public WaveLightingZoneV2(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId, ledId)
			{
			}

			void ILightingZoneEffect<SpectrumWaveEffect>.ApplyEffect(in SpectrumWaveEffect effect) => SetCurrentEffect(SpectrumWaveEffect.SharedInstance);
			bool ILightingZoneEffect<SpectrumWaveEffect>.TryGetCurrentEffect(out SpectrumWaveEffect effect) => CurrentEffect.TryGetEffect(out effect);
		}

		protected class ReactiveLightingZoneV2 : LightingZoneV2, ILightingZoneEffect<ReactiveEffect>
		{
			public ReactiveLightingZoneV2(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId, ledId)
			{
			}

			void ILightingZoneEffect<ReactiveEffect>.ApplyEffect(in ReactiveEffect effect) => SetCurrentEffect(effect);
			bool ILightingZoneEffect<ReactiveEffect>.TryGetCurrentEffect(out ReactiveEffect effect) => CurrentEffect.TryGetEffect(out effect);
		}

		protected class ReactiveWaveLightingZoneV2 : WaveLightingZoneV2, ILightingZoneEffect<ReactiveEffect>
		{
			public ReactiveWaveLightingZoneV2(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId, ledId)
			{
			}

			void ILightingZoneEffect<ReactiveEffect>.ApplyEffect(in ReactiveEffect effect) => SetCurrentEffect(effect);
			bool ILightingZoneEffect<ReactiveEffect>.TryGetCurrentEffect(out ReactiveEffect effect) => CurrentEffect.TryGetEffect(out effect);
		}

		protected class UnifiedBasicLightingZoneV2 : BasicLightingZoneV2, IUnifiedLightingFeature
		{
			public UnifiedBasicLightingZoneV2(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId, ledId)
			{
			}
		}

		protected class UnifiedWaveLightingZoneV2 : WaveLightingZoneV2, IUnifiedLightingFeature
		{
			public UnifiedWaveLightingZoneV2(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId, ledId)
			{
			}
		}

		protected class UnifiedReactiveLightingZoneV2 : ReactiveLightingZoneV2, IUnifiedLightingFeature
		{
			public UnifiedReactiveLightingZoneV2(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId, ledId)
			{
			}
		}

		protected class UnifiedReactiveWaveLightingZoneV2 : ReactiveWaveLightingZoneV2, IUnifiedLightingFeature
		{
			public UnifiedReactiveWaveLightingZoneV2(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId, ledId)
			{
			}
		}

		protected class BasicUnifiedLightingZoneV1 : UnifiedLightingZoneV1,
			ILightingZoneEffect<StaticColorEffect>,
			ILightingZoneEffect<MultiColorBreathingEffect>,
			ILightingZoneEffect<RandomColorBreathingEffect>,
			ILightingZoneEffect<SpectrumCycleEffect>,
			ILightingZoneEffect<SpectrumWaveEffect>,
			IUnifiedLightingFeature
		{
			public BasicUnifiedLightingZoneV1(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId, ledId)
			{
			}

			void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect) => SetCurrentEffect(effect);

			void ILightingZoneEffect<MultiColorBreathingEffect>.ApplyEffect(in MultiColorBreathingEffect effect)
			{
				if (effect.Colors.Count == 0) throw new ArgumentException(null, nameof(effect));
				SetCurrentEffect(effect);
			}

			void ILightingZoneEffect<RandomColorBreathingEffect>.ApplyEffect(in RandomColorBreathingEffect effect) => SetCurrentEffect(RandomColorBreathingEffect.SharedInstance);
			void ILightingZoneEffect<SpectrumCycleEffect>.ApplyEffect(in SpectrumCycleEffect effect) => SetCurrentEffect(SpectrumCycleEffect.SharedInstance);
			void ILightingZoneEffect<SpectrumWaveEffect>.ApplyEffect(in SpectrumWaveEffect effect) => SetCurrentEffect(SpectrumWaveEffect.SharedInstance);

			bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => CurrentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<MultiColorBreathingEffect>.TryGetCurrentEffect(out MultiColorBreathingEffect effect) => CurrentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<RandomColorBreathingEffect>.TryGetCurrentEffect(out RandomColorBreathingEffect effect) => CurrentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<SpectrumCycleEffect>.TryGetCurrentEffect(out SpectrumCycleEffect effect) => CurrentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<SpectrumWaveEffect>.TryGetCurrentEffect(out SpectrumWaveEffect effect) => CurrentEffect.TryGetEffect(out effect);
		}

		protected class ReactiveUnifiedLightingZoneV1 : BasicUnifiedLightingZoneV1, ILightingZoneEffect<ReactiveEffect>
		{
			public ReactiveUnifiedLightingZoneV1(BaseDevice device, Guid zoneId, RazerLedId ledId) : base(device, zoneId, ledId)
			{
			}

			void ILightingZoneEffect<ReactiveEffect>.ApplyEffect(in ReactiveEffect effect) => SetCurrentEffect(effect);
			bool ILightingZoneEffect<ReactiveEffect>.TryGetCurrentEffect(out ReactiveEffect effect) => CurrentEffect.TryGetEffect(out effect);
		}

		private sealed class LightingFeatureSet : IDeviceFeatureSet<ILightingDeviceFeature>
		{
			private readonly BaseDevice _device;

			public LightingFeatureSet(BaseDevice device) => _device = device;

			public ILightingDeviceFeature? this[Type type]
			{
				get
				{
					if (type == typeof(IUnifiedLightingFeature) && _device.HasUnifiedLighting)
					{
						return _device._unifiedLightingZone as IUnifiedLightingFeature;
					}

					return type == typeof(ILightingControllerFeature) && _device.HasLightingZones ||
						type == typeof(ILightingBrightnessFeature) && _device.HasUnifiedLighting ||
						type == typeof(ILightingPersistenceFeature) ||
						type == typeof(ILightingDeferredChangesFeature) ?
							_device :
							null;
				}
			}

			T? IDeviceFeatureSet<ILightingDeviceFeature>.GetFeature<T>() where T : class => Unsafe.As<T>(GetFeature<T>());

			private ILightingDeviceFeature? GetFeature<T>() where T : class
			{
				if (typeof(T) == typeof(IUnifiedLightingFeature) && _device.HasUnifiedLighting)
				{
					return _device._unifiedLightingZone as IUnifiedLightingFeature;
				}

				return typeof(T) == typeof(ILightingControllerFeature) && _device.HasLightingZones ||
					typeof(T) == typeof(ILightingBrightnessFeature) && _device.HasUnifiedLighting ||
					typeof(T) == typeof(ILightingPersistenceFeature) ||
					typeof(T) == typeof(ILightingDeferredChangesFeature) ?
						_device :
						null;
			}

			public bool IsEmpty => false;

			public int Count
			{
				get
				{
					int count = 2;

					if (_device.HasUnifiedLighting) count += 2;
					if (_device.HasLightingZones) count++;

					return count;
				}
			}

			public IEnumerator<KeyValuePair<Type, ILightingDeviceFeature>> GetEnumerator()
			{
				if (_device.HasUnifiedLighting)
				{
					yield return new(typeof(IUnifiedLightingFeature), (IUnifiedLightingFeature)_device._unifiedLightingZone!);
					yield return new(typeof(IBrightnessLightingEffect), _device);
				}
				if (_device.HasLightingZones)
				{
					yield return new(typeof(ILightingControllerFeature), _device);
				}
				yield return new(typeof(ILightingPersistenceFeature), _device);
				yield return new(typeof(ILightingDeferredChangesFeature), _device);
			}

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}

		// How do we use this ?
		private readonly byte _deviceIndex;
		private byte _lowPowerBatteryThreshold;
		private ushort _batteryLevelAndChargingStatus;
		private ushort _idleTimer;
		private readonly AsyncLock _lightingLock;
		private readonly Lock _batteryStateLock;
		private readonly LightingZone? _unifiedLightingZone;
		private readonly ImmutableArray<LightingZone> _lightingZones;
		private readonly IDeviceFeatureSet<IPowerManagementDeviceFeature> _powerManagementFeatures;
		private readonly IDeviceFeatureSet<ILightingDeviceFeature> _lightingFeatures;
		private bool _isUnifiedLightingEnabled;
		private byte _wirelessMaximumBrightness;

		protected bool HasUnifiedLighting => _unifiedLightingZone is not null;
		protected bool HasLightingZones => !_lightingZones.IsDefaultOrEmpty;

		protected bool HasBattery => (_deviceFlags & RazerDeviceFlags.HasBattery) != 0;
		protected bool HasWirelessMaximumBrightness => (_deviceFlags & RazerDeviceFlags.HasWirelessMaximumBrightness) != 0;
		protected bool HasLighting => (_deviceFlags & RazerDeviceFlags.HasLighting) != 0;
		protected bool HasLightingV2 => (_deviceFlags & RazerDeviceFlags.HasLightingV2) != 0;
		protected bool HasDpi => (_deviceFlags & RazerDeviceFlags.HasDpi) != 0;
		protected bool HasDpiPresets => (_deviceFlags & RazerDeviceFlags.HasDpiPresets) != 0;
		protected bool HasDpiPresetsRead => (_deviceFlags & RazerDeviceFlags.HasDpiPresetsRead) != 0;
		protected bool HasDpiPresetsV2 => (_deviceFlags & RazerDeviceFlags.HasDpiPresetsV2) != 0;
		protected bool HasReactiveLighting => (_deviceFlags & RazerDeviceFlags.HasReactiveLighting) != 0;
		protected bool MustSetDeviceMode3 => (_deviceFlags & RazerDeviceFlags.MustSetDeviceMode3) != 0;
		protected bool MustSetSensorState5 => (_deviceFlags & RazerDeviceFlags.MustSetSensorState5) != 0;
		protected bool IsWired => _connectionType == DeviceConnectionType.Wired;

		protected BaseDevice
		(
			IRazerProtocolTransport transport,
			RazerProtocolPeriodicEventGenerator? periodicEventGenerator,
			in DeviceInformation deviceInformation,
			ImmutableArray<RazerLedId> ledIds,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			DeviceConnectionType connectionType,
			ImmutableArray<DeviceId> deviceIds,
			byte mainDeviceIdIndex,
			RazerDeviceFlags deviceFlags
		) : base(transport, periodicEventGenerator, friendlyName, configurationKey, connectionType, deviceIds, mainDeviceIdIndex, deviceFlags)
		{
			_lightingLock = new();
			_batteryStateLock = new();
			_lowPowerBatteryThreshold = 1;
			_idleTimer = 1;

			(_unifiedLightingZone, _lightingZones) = CreateLightingZones(in deviceInformation, ledIds);

			// TODO: Better (No devices with proper multiple zones at the moment)
			_isUnifiedLightingEnabled = _unifiedLightingZone is not null && _lightingZones.Length == 0;

			_powerManagementFeatures = CreatePowerManagementFeatures();
			_lightingFeatures = CreateLightingFeatures();
		}

		protected virtual IDeviceFeatureSet<IPowerManagementDeviceFeature> CreatePowerManagementFeatures()
			=> HasBattery ?
				HasWirelessMaximumBrightness ?
					FeatureSet.Create<IPowerManagementDeviceFeature, BaseDevice, IBatteryStateDeviceFeature, IIdleSleepTimerFeature, ILowPowerModeBatteryThresholdFeature, IWirelessBrightnessFeature>(this) :
					FeatureSet.Create<IPowerManagementDeviceFeature, BaseDevice, IBatteryStateDeviceFeature, IIdleSleepTimerFeature, ILowPowerModeBatteryThresholdFeature>(this) :
				FeatureSet.Empty<IPowerManagementDeviceFeature>();

		protected virtual IDeviceFeatureSet<ILightingDeviceFeature> CreateLightingFeatures()
			=> HasLighting ? new LightingFeatureSet(this) : FeatureSet.Empty<ILightingDeviceFeature>();

		protected virtual (LightingZone? UnifiedLightingZone, ImmutableArray<LightingZone> LightingZones) CreateLightingZones(in DeviceInformation deviceInformation, ImmutableArray<RazerLedId> ledIds)
		{
			LightingZone? unifiedLightingZone = null;

			if (deviceInformation.HasLighting && deviceInformation.LightingZoneGuid is not null)
			{
				// NB: We don't support anything other than the unified lighting zone for now.
				// This code will need to be adapted to support multi zone devices if needed.
				// (NB: Mamba Chroma is technically a multi zone device, as its support is fused with the dock, but we expose the dock separately from the mouse)
				var lightingZoneGuid = deviceInformation.LightingZoneGuid.GetValueOrDefault();
				if (deviceInformation.HasLightingV2)
				{
					if (!ledIds.IsDefaultOrEmpty)
					{
						//var ledIds = await transport.GetLightingZoneIdsAsync(cancellationToken).ConfigureAwait(false);

						RazerLedId ledId = ledIds[0];

						unifiedLightingZone = deviceInformation.HasWaveLighting ?
							deviceInformation.HasReactiveLighting ?
								new UnifiedReactiveWaveLightingZoneV2(this, lightingZoneGuid, ledId) :
								new UnifiedWaveLightingZoneV2(this, lightingZoneGuid, ledId) :
							deviceInformation.HasReactiveLighting ?
								new UnifiedReactiveLightingZoneV2(this, lightingZoneGuid, ledId) :
								new UnifiedBasicLightingZoneV2(this, lightingZoneGuid, ledId);
					}
				}
				else
				{
					unifiedLightingZone = deviceInformation.HasReactiveLighting ?
						new ReactiveUnifiedLightingZoneV1(this, lightingZoneGuid, deviceInformation.MainLedId) :
						new BasicUnifiedLightingZoneV1(this, lightingZoneGuid, deviceInformation.MainLedId);
				}
			}
			return (unifiedLightingZone, []);
		}

		protected override async ValueTask InitializeAsync(CancellationToken cancellationToken)
		{
			if (HasWirelessMaximumBrightness)
			{
				_wirelessMaximumBrightness = await _transport.GetWirelessMaximumBrightnessAsync(cancellationToken).ConfigureAwait(false);
			}

			if (MustSetDeviceMode3)
			{
				await _transport.SetDeviceModeAsync(0x03, cancellationToken).ConfigureAwait(false);
			}

			await base.InitializeAsync(cancellationToken).ConfigureAwait(false);

			if (HasBattery)
			{
				ApplyBatteryLevelAndChargeStatusUpdate
				(
					3,
					await _transport.GetBatteryLevelAsync(cancellationToken).ConfigureAwait(false),
					await _transport.IsConnectedToExternalPowerAsync(cancellationToken).ConfigureAwait(false)
				);

				_lowPowerBatteryThreshold = await _transport.GetLowPowerThresholdAsync(cancellationToken).ConfigureAwait(false);
				_idleTimer = await _transport.GetIdleTimerAsync(cancellationToken).ConfigureAwait(false);
			}

			_periodicEventGenerator?.Register(this);

			if (_unifiedLightingZone is { })
			{
				await _unifiedLightingZone.InitializeAsync(1, cancellationToken).ConfigureAwait(false);
			}
			foreach (var zone in _lightingZones)
			{
				await zone.InitializeAsync(1, cancellationToken).ConfigureAwait(false);
			}
		}

		public override ValueTask DisposeAsync()
		{
			_periodicEventGenerator?.Unregister(this);
			return base.DisposeAsync();
		}

		protected override async ValueTask HandlePeriodicEventAsync()
		{
			if (HasBattery)
			{
				ApplyBatteryLevelAndChargeStatusUpdate
				(
					3,
					await _transport.GetBatteryLevelAsync(default).ConfigureAwait(false),
					await _transport.IsConnectedToExternalPowerAsync(default).ConfigureAwait(false)
				);
			}
		}

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

		IDeviceFeatureSet<IPowerManagementDeviceFeature> IDeviceDriver<IPowerManagementDeviceFeature>.Features => _powerManagementFeatures;
		IDeviceFeatureSet<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;

		IReadOnlyCollection<ILightingZone> ILightingControllerFeature.LightingZones => ImmutableCollectionsMarshal.AsArray(_lightingZones)!;

		LightingPersistenceMode ILightingPersistenceFeature.PersistenceMode => LightingPersistenceMode.CanPersist;
		bool ILightingPersistenceFeature.HasDeviceManagedLighting => false;
		bool ILightingPersistenceFeature.HasDynamicPresence => false;

		ValueTask ILightingDeferredChangesFeature.ApplyChangesAsync(bool shouldPersist) => ApplyChangesAsync(shouldPersist, default);

		byte ILightingBrightnessFeature.MaximumBrightness => 255;
		byte ILightingBrightnessFeature.CurrentBrightness
		{
			get => (_unifiedLightingZone as LightingZone)?.GetBrightness() ?? 0;
			set { if (_unifiedLightingZone is LightingZone lz) lz.SetBrightness(value); }
		}

		private async ValueTask ApplyChangesAsync(bool shouldPersist, CancellationToken cancellationToken)
		{
			using (await _lightingLock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				await ApplyLightingChangesAsync(shouldPersist, cancellationToken).ConfigureAwait(false);
			}
		}

		protected virtual async ValueTask ApplyLightingChangesAsync(bool shouldPersist, CancellationToken cancellationToken)
		{
			if (_isUnifiedLightingEnabled)
			{
				await _unifiedLightingZone!.ApplyAsync(shouldPersist ? (byte)1 : (byte)0, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				foreach (var zone in _lightingZones)
				{
					await zone.ApplyAsync(shouldPersist ? (byte)1 : (byte)0, cancellationToken).ConfigureAwait(false);
				}
			}
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

		// The device should actually allow anything from 0 to 65535 seconds (although not tested), but Razer Synapse 3 only show 1 to 15 minutes ranges, which seems a reasonable limitation.
		TimeSpan IIdleSleepTimerFeature.MinimumIdleTime => TimeSpan.FromTicks(1 * TimeSpan.TicksPerMinute);
		TimeSpan IIdleSleepTimerFeature.MaximumIdleTime => TimeSpan.FromTicks(15 * TimeSpan.TicksPerMinute);
		TimeSpan IIdleSleepTimerFeature.IdleTime => TimeSpan.FromTicks(_idleTimer * TimeSpan.TicksPerSecond);

		async Task IIdleSleepTimerFeature.SetIdleTimeAsync(TimeSpan idleTime, CancellationToken cancellationToken)
		{
			if (idleTime < TimeSpan.FromTicks(TimeSpan.TicksPerSecond) || idleTime > TimeSpan.FromTicks(65535 * TimeSpan.TicksPerSecond)) throw new ArgumentOutOfRangeException(nameof(idleTime));
			await _transport.SetIdleTimerAsync(checked((ushort)idleTime.TotalSeconds), cancellationToken).ConfigureAwait(false);
		}

		Half ILowPowerModeBatteryThresholdFeature.LowPowerThreshold => (Half)(_lowPowerBatteryThreshold / 255f);

		Task ILowPowerModeBatteryThresholdFeature.SetLowPowerBatteryThresholdAsync(Half lowPowerThreshold, CancellationToken cancellationToken)
			=> _transport.SetLowPowerThresholdAsync((byte)(255 * lowPowerThreshold), cancellationToken);

		byte IWirelessBrightnessFeature.MinimumValue => 4;
		byte IWirelessBrightnessFeature.MaximumValue => 255;

		byte IWirelessBrightnessFeature.WirelessBrightness => _wirelessMaximumBrightness;

		async Task IWirelessBrightnessFeature.SetWirelessBrightnessAsync(byte maximumBrightness, CancellationToken cancellationToken)
		{
			await _transport.SetWirelessMaximumBrightnessAsync(maximumBrightness, cancellationToken);
			_wirelessMaximumBrightness = maximumBrightness;
		}
	}
}
