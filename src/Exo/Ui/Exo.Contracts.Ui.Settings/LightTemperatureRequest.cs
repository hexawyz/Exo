﻿using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class LightTemperatureRequest
{
	[DataMember(Order = 1)]
	public Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public Guid LightId { get; init; }
	[DataMember(Order = 3)]
	public uint Temperature { get; init; }
}
