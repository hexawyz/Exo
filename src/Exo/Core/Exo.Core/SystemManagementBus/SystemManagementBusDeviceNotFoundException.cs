namespace Exo.SystemManagementBus;

public sealed class SystemManagementBusDeviceNotFoundException : SystemManagementBusException
{
	public byte? Address { get; }

	public SystemManagementBusDeviceNotFoundException()
		: this("The SMBus device was not found at the specified address.")
	{
	}

	public SystemManagementBusDeviceNotFoundException(byte address)
		: this($"The SMBus device was not found at address {address}.", address)
	{
	}

	public SystemManagementBusDeviceNotFoundException(string? message) : base(message)
	{
	}

	public SystemManagementBusDeviceNotFoundException(string? message, byte address) : base(message)
	{
		Address = address;
	}

	public SystemManagementBusDeviceNotFoundException(string? message, Exception? innerException) : base(message, innerException)
	{
	}

	public SystemManagementBusDeviceNotFoundException(string? message, byte address, Exception? innerException) : base(message, innerException)
	{
		Address = address;
	}
}
