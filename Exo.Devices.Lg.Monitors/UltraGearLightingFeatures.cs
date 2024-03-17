using Exo.ColorFormats;
using Exo.Devices.Lg.Monitors.LightingEffects;
using Exo.Features.LightingFeatures;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Lg.Monitors;

// TODO: This probably needs to be adapted to support other monitors, but hopefully it should be mostly the same protocol.
internal sealed class UltraGearLightingFeatures :
	IAsyncDisposable,
	IUnifiedLightingFeature,
	ILightingDeferredChangesFeature,
	ILightingBrightnessFeature,
	IAddressableLightingZone<RgbColor>,
	ILightingZoneEffect<DisabledEffect>,
	//ILightingZoneEffect<StaticColorEffect>,
	ILightingZoneEffect<StaticColorPreset1Effect>,
	ILightingZoneEffect<StaticColorPreset2Effect>,
	ILightingZoneEffect<StaticColorPreset3Effect>,
	ILightingZoneEffect<StaticColorPreset4Effect>,
	ILightingZoneEffect<SpectrumCycleEffect>,
	ILightingZoneEffect<SpectrumWaveEffect>
{
	private static readonly Guid LightingZoneGuid = new(0x7105A4FA, 0x2235, 0x49FC, 0xA7, 0x5A, 0xFD, 0x0D, 0xEC, 0x13, 0x51, 0x99);

	private const int StateEffectChanged = 0x01;
	private const int StateBrightnessChanged = 0x02;
	private const int StateLocked = 0x100;

	private readonly UltraGearLightingTransport _lightingTransport;
	private ILightingEffect _currentEffect;
	// Protect against concurrent accesses using a combination of lock and state.
	// Concurrent accesses should not happen, but I believe this is not enforced anywhere yet. (i.e. In the lighting service)
	// The protection here is quite simple and will prevent incorrect uses at the cost of raising exceptions.
	// The state value can only change within the lock, and if the state is locked, an exception must be thrown.
	// The state is locked when the ApplyChangesAsync method is executing, and unlocked afterwards.
	private readonly object _lock;
	private int _state;
	private readonly byte _ledCount;
	private byte _currentBrightness;
	private readonly byte _minimumBrightness;
	private readonly byte _maximumBrightness;

	public UltraGearLightingFeatures
	(
		UltraGearLightingTransport lightingTransport,
		byte ledCount,
		LightingEffect activeEffect,
		byte currentBrightness,
		byte minimumBrightness,
		byte maximumBrightness
	)
	{
		_lightingTransport = lightingTransport;
		_currentEffect = activeEffect switch
		{
			LightingEffect.Static1 => StaticColorPreset1Effect.SharedInstance,
			LightingEffect.Static2 => StaticColorPreset2Effect.SharedInstance,
			LightingEffect.Static3 => StaticColorPreset3Effect.SharedInstance,
			LightingEffect.Static4 => StaticColorPreset4Effect.SharedInstance,
			LightingEffect.Peaceful => SpectrumCycleEffect.SharedInstance,
			LightingEffect.Dynamic => SpectrumWaveEffect.SharedInstance,
			// I'm unsure what would happen here if the current effect was reported as audio sync or video sync ?
			// If these modes are reported, we need to explicitly disable the lighting.
			_ => DisabledEffect.SharedInstance,
		};
		_lock = new();
		_ledCount = ledCount;
		_currentBrightness = currentBrightness;
		_minimumBrightness = minimumBrightness;
		_maximumBrightness = maximumBrightness;
	}

	public async ValueTask DisposeAsync()
	{
		await _lightingTransport.DisposeAsync().ConfigureAwait(false);
	}

	bool IUnifiedLightingFeature.IsUnifiedLightingEnabled => true;

	Guid ILightingZone.ZoneId => LightingZoneGuid;

	ILightingEffect ILightingZone.GetCurrentEffect() => _currentEffect;

	void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect) => CurrentEffect = DisabledEffect.SharedInstance;
	//void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect) => CurrentEffect = effect;
	void ILightingZoneEffect<StaticColorPreset1Effect>.ApplyEffect(in StaticColorPreset1Effect effect) => CurrentEffect = StaticColorPreset1Effect.SharedInstance;
	void ILightingZoneEffect<StaticColorPreset2Effect>.ApplyEffect(in StaticColorPreset2Effect effect) => CurrentEffect = StaticColorPreset2Effect.SharedInstance;
	void ILightingZoneEffect<StaticColorPreset3Effect>.ApplyEffect(in StaticColorPreset3Effect effect) => CurrentEffect = StaticColorPreset3Effect.SharedInstance;
	void ILightingZoneEffect<StaticColorPreset4Effect>.ApplyEffect(in StaticColorPreset4Effect effect) => CurrentEffect = StaticColorPreset4Effect.SharedInstance;
	void ILightingZoneEffect<SpectrumCycleEffect>.ApplyEffect(in SpectrumCycleEffect effect) => CurrentEffect = SpectrumCycleEffect.SharedInstance;
	void ILightingZoneEffect<SpectrumWaveEffect>.ApplyEffect(in SpectrumWaveEffect effect) => CurrentEffect = SpectrumWaveEffect.SharedInstance;
	void ILightingZoneEffect<AddressableColorEffect>.ApplyEffect(in AddressableColorEffect effect) => CurrentEffect = AddressableColorEffect.SharedInstance;

	bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => CurrentEffect.TryGetEffect(out effect);
	//bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<SpectrumCycleEffect>.TryGetCurrentEffect(out SpectrumCycleEffect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<SpectrumWaveEffect>.TryGetCurrentEffect(out SpectrumWaveEffect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<StaticColorPreset1Effect>.TryGetCurrentEffect(out StaticColorPreset1Effect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<StaticColorPreset2Effect>.TryGetCurrentEffect(out StaticColorPreset2Effect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<StaticColorPreset3Effect>.TryGetCurrentEffect(out StaticColorPreset3Effect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<StaticColorPreset4Effect>.TryGetCurrentEffect(out StaticColorPreset4Effect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<AddressableColorEffect>.TryGetCurrentEffect(out AddressableColorEffect effect) => CurrentEffect.TryGetEffect(out effect);

	int IAddressableLightingZone.AddressableLightCount => _ledCount;
	bool IAddressableLightingZone.AllowsRandomAccesses => false;

	ValueTask IAddressableLightingZone<RgbColor>.SetColorsAsync(int index, ReadOnlySpan<RgbColor> colors)
	{
		if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
		if (colors.Length != _ledCount) throw new ArgumentException("The number of colors received is incorrect.");

		return new ValueTask(_lightingTransport.SetVideoSyncColors(colors, default));
	}

	byte ILightingBrightnessFeature.MinimumBrightness => _minimumBrightness;
	byte ILightingBrightnessFeature.MaximumBrightness => _maximumBrightness;

	byte ILightingBrightnessFeature.CurrentBrightness
	{
		get => _currentBrightness;
		set
		{
			if (value < _minimumBrightness || value > _maximumBrightness) throw new ArgumentOutOfRangeException(nameof(value));

			lock (_lock)
			{
				EnsureStateIsUnlocked();
				_currentBrightness = value;
				_state |= StateBrightnessChanged;
			}
		}
	}

	ValueTask ILightingDeferredChangesFeature.ApplyChangesAsync()
	{
		int state;

		lock (_lock)
		{
			EnsureStateIsUnlocked();

			if (_state == 0) return ValueTask.CompletedTask;

			state = _state;

			_state = state | StateLocked;
		}

		return new ValueTask(ApplyChangesAsyncCore(state));
	}

	// This method does not actually execute from within the lock (we would need an async lock), but the state has the lock flag preventing concurrent accesses.
	private async Task ApplyChangesAsyncCore(int state)
	{
		if ((state & StateBrightnessChanged) != 0)
		{
			await _lightingTransport.SetLightingStatusAsync(_currentEffect is not DisabledEffect, _currentBrightness, default).ConfigureAwait(false);
		}

		if ((state & StateEffectChanged) != 0)
		{
			switch (CurrentEffect)
			{
			case DisabledEffect:
				// This would preserve the current lighting, so it might be worth to consider setting a static color effect here, although the LG app does not do this.
				await _lightingTransport.SetLightingStatusAsync(false, 0, default).ConfigureAwait(false);
				break;
			//case StaticColorEffect staticColor:
			//	// We implement the static color effect using the video sync mode, which is essentially addressable mode.
			//	// Maybe there are subtle differences with true addressable mode, but I'm not aware of them. (Is there even any actual difference between audio & video modes ?)
			//	await _lightingTransport.SetActiveEffectAsync(LightingEffect.VideoSync, default).ConfigureAwait(false);
			//	await _lightingTransport.EnableLightingEffectAsync(LightingEffect.VideoSync, default).ConfigureAwait(false);
			//	await _lightingTransport.SetVideoSyncColors(staticColor.Color, 36, default).ConfigureAwait(false);
			//	break;
			case StaticColorPreset1Effect:
				await _lightingTransport.SetActiveEffectAsync(LightingEffect.Static1, default).ConfigureAwait(false);
				await _lightingTransport.EnableLightingEffectAsync(LightingEffect.Static1, default).ConfigureAwait(false);
				break;
			case StaticColorPreset2Effect:
				await _lightingTransport.SetActiveEffectAsync(LightingEffect.Static2, default).ConfigureAwait(false);
				await _lightingTransport.EnableLightingEffectAsync(LightingEffect.Static2, default).ConfigureAwait(false);
				break;
			case StaticColorPreset3Effect:
				await _lightingTransport.SetActiveEffectAsync(LightingEffect.Static3, default).ConfigureAwait(false);
				await _lightingTransport.EnableLightingEffectAsync(LightingEffect.Static3, default).ConfigureAwait(false);
				break;
			case StaticColorPreset4Effect:
				await _lightingTransport.SetActiveEffectAsync(LightingEffect.Static4, default).ConfigureAwait(false);
				await _lightingTransport.EnableLightingEffectAsync(LightingEffect.Static4, default).ConfigureAwait(false);
				break;
			case SpectrumCycleEffect:
				await _lightingTransport.SetActiveEffectAsync(LightingEffect.Peaceful, default).ConfigureAwait(false);
				await _lightingTransport.EnableLightingEffectAsync(LightingEffect.Peaceful, default).ConfigureAwait(false);
				break;
			case SpectrumWaveEffect:
				await _lightingTransport.SetActiveEffectAsync(LightingEffect.Dynamic, default).ConfigureAwait(false);
				await _lightingTransport.EnableLightingEffectAsync(LightingEffect.Dynamic, default).ConfigureAwait(false);
				break;
			case AddressableColorEffect:
				// NB: It seems that the dynamic effect will self-disable after a while if not updated. (10s)
				// TODO: Find an acceptable way to manage this. Either force keep-alive the effect or track long delays between updates to re-enable the effect.
				await _lightingTransport.SetActiveEffectAsync(LightingEffect.VideoSync, default).ConfigureAwait(false);
				await _lightingTransport.EnableLightingEffectAsync(LightingEffect.VideoSync, default).ConfigureAwait(false);

				//_ = TestDynamicEffectAsync();

				break;
			}
		}
		Volatile.Write(ref _state, 0);
	}

	// Just used to validate that the effect works.
	// This is a very basic effect that should probably be moved into its own class later.
	private async Task TestDynamicEffectAsync()
	{
		var colors = new RgbColor[40];

		for (int i = 0; i < 10; i++)
		{
			int j = 4 * i;
			colors[j + 0] = new(0x00, 0x40, 0x40);
			colors[j + 1] = new(0x00, 0xFF, 0xFF);
			colors[j + 2] = new(0x00, 0x40, 0x40);
			colors[j + 3] = new(0x00, 0x00, 0x00);
		}

		int k = 0;
		while (true)
		{
			await this.SetColorsAsync(colors.AsSpan(k, 36));
			k = (k + 1) & 3;
			await Task.Delay(150);
		}
	}

	private ILightingEffect CurrentEffect
	{
		get => Volatile.Read(ref _currentEffect);
		set
		{
			lock (_lock)
			{
				EnsureStateIsUnlocked();
				_currentEffect = value;
				_state |= StateEffectChanged;
			}
		}
	}

	private void EnsureStateIsUnlocked()
	{
		if ((_state & StateLocked) != 0) throw new InvalidOperationException("A concurrent operation is currently being executed.");
	}
}
