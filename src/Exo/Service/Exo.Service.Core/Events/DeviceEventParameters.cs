using System.ComponentModel;
using System.Runtime.Serialization;
using Exo.Programming;

namespace Exo.Service.Events;

[DataContract]
[TypeId(0x1C7E9E60, 0xC9E0, 0x444D, 0xA9, 0x77, 0xD1, 0x2A, 0x0E, 0x0E, 0x82, 0x57)]
public class DeviceEventParameters : EventParameters
{
	public DeviceEventParameters(DeviceId deviceId) => DeviceId = deviceId;

	[DataMember(Order = 1)]
	[Description("The device that triggered the event.")]
	public DeviceId DeviceId { get; }
}
