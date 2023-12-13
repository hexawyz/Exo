using System.Runtime.CompilerServices;

namespace Exo.Programming;

/// <summary>Represents a Device ID.</summary>
/// <remarks>
/// Device IDs are generally expressed as GUID values, which is <c>not</c> an implementation detail.
/// This type act as a wrapper to provide a strongly typed programming model.
/// It will avoid confusion when dealing with the many GUID types in the programming model, and clearly express when the value is supposed to point to a device.
/// For example, the UI will be able to display the actual name of the device in place of the GUID value, and validate that the user does not input a random value.
/// </remarks>
public readonly struct DeviceId
{
	private readonly Guid _value;

	public static implicit operator Guid(DeviceId deviceId) => deviceId._value;
	public static explicit operator DeviceId(Guid guid) => Unsafe.As<Guid, DeviceId>(ref guid);

	public override string ToString() => _value.ToString();
}
