using System.Runtime.CompilerServices;

namespace Exo.Programming;

/// <summary>Represents a Device ID.</summary>
/// <remarks>
/// Device IDs are generally expressed as GUID values, which is <c>not</c> an implementation detail.
/// This type act as a wrapper to provide a strongly typed programming model.
/// It will avoid confusion when dealing with the many GUID types in the programming model, and clearly express when the value is supposed to point to a device.
/// For example, the UI will be able to display the actual name of the device in place of the GUID value, and validate that the user does not input a random value.
/// </remarks>
[TypeId(0x7B175861, 0x756B, 0x4D85, 0xA7, 0x69, 0x7B, 0x03, 0x1D, 0x4D, 0x41, 0x7F)]
public readonly struct DeviceId
{
	private readonly Guid _value;

	public static implicit operator Guid(DeviceId deviceId) => deviceId._value;
	public static explicit operator DeviceId(Guid guid) => Unsafe.As<Guid, DeviceId>(ref guid);

	public override string ToString() => _value.ToString();
}
