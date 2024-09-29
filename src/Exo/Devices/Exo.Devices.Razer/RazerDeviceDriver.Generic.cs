using System.Collections.Immutable;
using DeviceTools;

namespace Exo.Devices.Razer;

public abstract partial class RazerDeviceDriver
{
	private class Generic : BaseDevice
	{
		public override DeviceCategory DeviceCategory { get; }

		public Generic(
			IRazerProtocolTransport transport,
			RazerProtocolPeriodicEventGenerator? periodicEventGenerator,
			DeviceCategory deviceCategory,
			in DeviceInformation deviceInformation,
			ImmutableArray<RazerLedId> ledIds,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			RazerDeviceFlags deviceFlags,
			ImmutableArray<DeviceId> deviceIds,
			byte mainDeviceIdIndex
		) : base(transport, periodicEventGenerator, deviceInformation, ledIds, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
		{
			DeviceCategory = deviceCategory;
		}
	}
}
