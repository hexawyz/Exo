using System.Diagnostics;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;

[DebuggerDisplay("[{Index,h}] {Feature} V{Version,d} ({Type})")]
public readonly struct HidPlusPlusFeatureInformation
{
	public HidPlusPlusFeatureInformation(byte index, HidPlusPlusFeature feature, HidPlusPlusFeatureTypes type, byte version)
	{
		Index = index;
		Feature = feature;
		Type = type;
		Version = version;
	}

	public byte Index { get; }
	public HidPlusPlusFeature Feature { get; }
	public HidPlusPlusFeatureTypes Type { get; }
	public byte Version { get; }
}
