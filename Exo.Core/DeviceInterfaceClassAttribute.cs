using System;

namespace Exo;

/// <summary>Declares the device interface class supported by the specified attribute.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DeviceInterfaceClassAttribute : Attribute
{
	public Guid DeviceInterfaceClassGuid { get; }

	public DeviceInterfaceClassAttribute(DeviceInterfaceClass deviceInterfaceClass) : this(deviceInterfaceClass.ToGuid()) { }

	public DeviceInterfaceClassAttribute(string deviceInterfaceClassGuid) : this(Guid.Parse(deviceInterfaceClassGuid)) { }

	private DeviceInterfaceClassAttribute(Guid deviceInterfaceClassGuid) => DeviceInterfaceClassGuid = deviceInterfaceClassGuid;
}
