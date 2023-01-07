using Exo.Core.Features;

namespace Exo.Core;

// TODO: We need something to support various overlay notifications for any keyboard, or at least globally on Windows.
// Not sure yet if we want this driver to be the generic "all keyboards" driver or for it to be instanciated for each keyboard.
// Ideally, we want to plug notifications for a few specific keypresses, but that may not be possible without some helper applications on UI-side anyway.
public sealed class GenericKeyboardDriver : Driver, IDeviceDriver<IKeyboardDeviceFeature>
{
	public override string FriendlyName => "Keyboard";

	protected override string GetConfigurationKey() => throw new System.NotImplementedException();

	public override IDeviceFeatureCollection<IDeviceFeature> Features => FeatureCollection<IDeviceFeature>.Empty();

	IDeviceFeatureCollection<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => FeatureCollection<IKeyboardDeviceFeature>.Empty();
}
