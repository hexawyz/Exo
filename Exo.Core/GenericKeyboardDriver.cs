using System.Threading.Tasks;
using Exo.Features;

namespace Exo;

// TODO: We need something to support various overlay notifications for any keyboard, or at least globally on Windows.
// Not sure yet if we want this driver to be the generic "all keyboards" driver or for it to be instantiated for each keyboard.
// Ideally, we want to plug notifications for a few specific keypresses, but that may not be possible without some helper applications on UI-side anyway.
public sealed class GenericKeyboardDriver : Driver, IDeviceDriver<IKeyboardDeviceFeature>
{
	public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

	private GenericKeyboardDriver(string friendlyName, DeviceConfigurationKey configurationKey) : base(friendlyName, configurationKey)
	{
	}

	IDeviceFeatureSet<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => FeatureSet<IKeyboardDeviceFeature>.Empty();

	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
