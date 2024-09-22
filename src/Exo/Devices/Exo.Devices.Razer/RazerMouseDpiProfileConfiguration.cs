using System.Collections.Immutable;

namespace Exo.Devices.Razer;

public readonly struct RazerMouseDpiProfileConfiguration
{
	public RazerMouseDpiProfileConfiguration(byte activePresetIndex, ImmutableArray<RazerMouseDpiPreset> presets)
	{
		// NB: We'll do some argument validation here, but we'll tolerate 0 as a valid active profile just in case. (e.g. what if DPI is set manually to a non-profile value ?)
		if (presets.IsDefault) throw new ArgumentNullException(nameof(presets));
		if (activePresetIndex > presets.Length) throw new ArgumentOutOfRangeException(nameof(activePresetIndex));

		ActivePresetIndex = activePresetIndex;
		Presets = presets;
	}

	public byte ActivePresetIndex { get; }
	public ImmutableArray<RazerMouseDpiPreset> Presets { get;}
}
