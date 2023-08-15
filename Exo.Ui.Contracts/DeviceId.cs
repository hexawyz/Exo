using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class DeviceId
{
	/// <summary>Gets the source for the Device ID information, if known.</summary>
	[DataMember(Order = 1)]
	public DeviceIdSource Source { get; init; }

	/// <summary>Gets the source for the Vendor ID information, if known.</summary>
	[DataMember(Order = 2)]
	public VendorIdSource VendorIdSource { get; init; }

	/// <summary>Gets the vendor ID of the device.</summary>
	[DataMember(Order = 3)]
	public required ushort VendorId { get; init; }

	/// <summary>Gets the product ID of the device.</summary>
	[DataMember(Order = 4)]
	public required ushort ProductId { get; init; }

	/// <summary>Gets the Version of the device.</summary>
	/// <remarks>This will be <c>0xFFFF</c> if unknown.</remarks>
	[DataMember(Order = 5)]
	public required ushort Version { get; init; }
}
