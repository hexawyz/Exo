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
			DeviceCategory deviceCategory,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			RazerDeviceFlags deviceFlags,
			ImmutableArray<DeviceId> deviceIds,
			byte mainDeviceIdIndex
		) : base(transport, lightingZoneId, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
		{
			DeviceCategory = deviceCategory;
		}
	}
}
