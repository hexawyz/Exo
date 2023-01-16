using System;

namespace Exo
{
	/// <summary>Declares the device interface class supported by the specified attribute.</summary>
	[AttributeUsage(AttributeTargets.Class)]
	public sealed class DeviceInterfaceClassAttribute : Attribute
	{
		public Guid DeviceInterfaceClassGuid { get; }

		public DeviceInterfaceClassAttribute(Guid deviceInterfaceClassGuid) => DeviceInterfaceClassGuid = deviceInterfaceClassGuid;
	}
}
