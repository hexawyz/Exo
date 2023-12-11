using System.ComponentModel;
using System.Runtime.Serialization;
using Exo.Programming;

namespace Exo.Service;

[DataContract]
public class DeviceEventParameters : EventParameters
{
	public DeviceEventParameters(Guid deviceId) => DeviceId = deviceId;

	[DataMember(Order = 1)]
	[Description("The device that triggered the event.")]
	public Guid DeviceId { get; }
}
