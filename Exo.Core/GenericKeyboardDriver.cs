using Exo.Features;

namespace Exo;

// TODO: We need something to support various overlay notifications for any keyboard, or at least globally on Windows.
// Not sure yet if we want this driver to be the generic "all keyboards" driver or for it to be instanciated for each keyboard.
// Ideally, we want to plug notifications for a few specific keypresses, but that may not be possible without some helper applications on UI-side anyway.
public sealed class GenericKeyboardDriver : Driver, IDeviceDriver<IKeyboardDeviceFeature>
{
	private GenericKeyboardDriver(string friendlyName, DeviceConfigurationKey configurationKey) : base(friendlyName, configurationKey)
	{
	}

	public override IDeviceFeatureCollection<IDeviceFeature> Features => FeatureCollection<IDeviceFeature>.Empty();

	IDeviceFeatureCollection<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => FeatureCollection<IKeyboardDeviceFeature>.Empty();
}
