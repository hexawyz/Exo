using System.Collections.Immutable;
using DeviceTools.Logitech.HidPlusPlus;

namespace Exo.Devices.Logitech;

public interface IHidPlusPlusDeviceFeature : IDeviceFeature
{
	HidPlusPlusVersion ProtocolVersion { get; }
	ImmutableArray<HidPlusPlusFeature> SupportedFeatures { get; }
}
