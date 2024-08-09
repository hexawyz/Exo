using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class MultiDeviceLightingUpdates
{
	private readonly ImmutableArray<DeviceLightingUpdate> _deviceUpdates;
	[DataMember(Order = 1)]
	public required ImmutableArray<DeviceLightingUpdate> DeviceUpdates
	{
		get => _deviceUpdates.NotNull();
		init => _deviceUpdates = value.NotNull();
	}
}
