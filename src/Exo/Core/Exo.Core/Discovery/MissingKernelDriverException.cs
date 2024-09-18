namespace Exo.Discovery;

public sealed class MissingKernelDriverException : Exception
{
	public string? DeviceName { get; }

	public MissingKernelDriverException()
		: base("A kernel driver driver is missing to work with the device.")
	{
	}

	public MissingKernelDriverException(string? deviceName) : this(deviceName, $"A kernel driver is missing to work with the device {deviceName}.")
	{
	}

	public MissingKernelDriverException(string? deviceName, string? message) : base(message)
	{
		DeviceName = deviceName;
	}

	public MissingKernelDriverException(string? deviceName, string? message, Exception? innerException) : base(message, innerException)
	{
		DeviceName = deviceName;
	}
}
