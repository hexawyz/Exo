using DeviceTools;

namespace Exo.Discovery;

public sealed class MissingKernelDriverException : Exception
{
	private static string GetMessage(string? deviceName)
		=> deviceName is { Length: > 0 } ? $"A kernel driver is missing to work with the device {deviceName}." : "A kernel driver driver is missing to work with the device.";

	public string? DeviceName { get; }
	public DeviceId? DeviceId { get; }

	public MissingKernelDriverException()
		: this(null, null)
	{
	}

	public MissingKernelDriverException(string? deviceName)
		: this(deviceName, null)
	{
	}

	public MissingKernelDriverException(DeviceId? deviceId)
		: this(null, deviceId)
	{
	}

	public MissingKernelDriverException(string? deviceName, DeviceId? deviceId)
		: this(deviceName, deviceId, GetMessage(deviceName))
	{
	}

	public MissingKernelDriverException(string? deviceName, DeviceId? deviceId, string? message) : base(message)
	{
		DeviceName = deviceName;
		DeviceId = deviceId;
	}

	public MissingKernelDriverException(string? deviceName, DeviceId? deviceId, Exception? innerException)
		: this(deviceName, deviceId, GetMessage(deviceName), innerException)
	{
	}

	public MissingKernelDriverException(string? deviceName, DeviceId? deviceId, string? message, Exception? innerException) : base(message, innerException)
	{
		DeviceName = deviceName;
		DeviceId = deviceId;
	}
}
