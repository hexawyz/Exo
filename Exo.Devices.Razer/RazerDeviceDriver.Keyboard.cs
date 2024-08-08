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
			RazerProtocolPeriodicEventGenerator periodicEventGenerator,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			RazerDeviceFlags deviceFlags,
			ImmutableArray<DeviceId> deviceIds,
			byte mainDeviceIdIndex
		) : base(transport, periodicEventGenerator, lightingZoneId, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
		{
		}
	}
}
