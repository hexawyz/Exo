using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Exo.Service.Configuration;

[TypeId(0xBB4A71CB, 0x2894, 0x4388, 0xAE, 0x06, 0x40, 0x06, 0x03, 0x0F, 0x23, 0xBF)]
internal readonly struct PersistedMouseInformation
{
	[JsonConstructor]
	public PersistedMouseInformation
	(
		DotsPerInch maximumDpi,
		MouseCapabilities capabilities,
		byte profileCount,
		byte minimumDpiPresetCount,
		byte maximumDpiPresetCount,
		ImmutableArray<ushort> supportedPollingFrequencies
	)
	{
		MaximumDpi = maximumDpi;
		Capabilities = capabilities;
		ProfileCount = profileCount;
		MinimumDpiPresetCount = minimumDpiPresetCount;
		MaximumDpiPresetCount = maximumDpiPresetCount;
		SupportedPollingFrequencies = supportedPollingFrequencies.IsDefaultOrEmpty ? [] : supportedPollingFrequencies;
	}

	public DotsPerInch MaximumDpi { get; }
	public MouseCapabilities Capabilities { get; }
	public byte ProfileCount { get; }
	public byte MinimumDpiPresetCount { get; }
	public byte MaximumDpiPresetCount { get; }
	public ImmutableArray<ushort> SupportedPollingFrequencies { get; }
}
