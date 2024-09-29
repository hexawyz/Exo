using System.Collections.Immutable;
using DeviceTools;

namespace Exo.Devices.Razer;

public abstract partial class RazerDeviceDriver
{
	private class Keyboard : BaseDevice
	{
		public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

		public Keyboard(
			IRazerProtocolTransport transport,
			RazerProtocolPeriodicEventGenerator? periodicEventGenerator,
			in DeviceInformation deviceInformation,
			ImmutableArray<RazerLedId> ledIds,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			RazerDeviceFlags deviceFlags,
			ImmutableArray<DeviceId> deviceIds,
			byte mainDeviceIdIndex
		) : base(transport, periodicEventGenerator, deviceInformation, ledIds, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
		{
		}
	}
}
