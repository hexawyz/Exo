using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

/// <summary>Represents device information containing extra feature information.</summary>
[DataContract]
public sealed class ExtendedDeviceInformation
{
	/// <summary>Gets the standard device ID, if available through the device ID feature.</summary>
	/// <remarks>
	/// <para>While many devices have a unique ID in the standard Vendor ID / Product ID / Version scheme, not all of them do.</para>
	/// <para>
	/// The device ID cannot be used as a substitute for <see cref="DeviceInformation.Id"/>.
	/// It only provides information on the device but is not tied to the specific device instance.
	/// </para>
	/// </remarks>
	[DataMember(Order = 1)]
	public DeviceId? DeviceId { get; init; }

	/// <summary>Gets the serial number exposed by the serial number feature, if available.</summary>
	[DataMember(Order = 2)]
	public string? SerialNumber { get; init; }

	/// <summary>Indicates if the device has a battery that can report its level.</summary>
	[DataMember(Order = 3)]
	public bool HasBatteryLevel { get; init; }
}
