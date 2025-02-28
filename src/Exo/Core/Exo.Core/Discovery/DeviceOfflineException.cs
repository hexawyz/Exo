using DeviceTools;

namespace Exo.Discovery;

/// <summary>This exception can be thrown in driver factory methods to indicate that the device has become offline before initialization completed.</summary>
public sealed class DeviceOfflineException : Exception
{
	private static string GetMessage(string? deviceName)
		=> deviceName is { Length: > 0 } ? $"The device {deviceName} is offline." : "The device is offline.";

	public string? DeviceName { get; }
	public DeviceId? DeviceId { get; }

	public DeviceOfflineException()
		: this(null, null)
	{
	}

	public DeviceOfflineException(string? deviceName)
		: this(deviceName, null)
	{
	}

	public DeviceOfflineException(DeviceId? deviceId)
		: this(null, deviceId)
	{
	}

	public DeviceOfflineException(string? deviceName, DeviceId? deviceId)
		: this(deviceName, deviceId, GetMessage(deviceName))
	{
	}

	public DeviceOfflineException(string? deviceName, DeviceId? deviceId, string? message) : base(message)
	{
		DeviceName = deviceName;
		DeviceId = deviceId;
	}

	public DeviceOfflineException(string? deviceName, DeviceId? deviceId, Exception? innerException)
		: this(deviceName, deviceId, GetMessage(deviceName), innerException)
	{
	}

	public DeviceOfflineException(string? deviceName, DeviceId? deviceId, string? message, Exception? innerException) : base(message, innerException)
	{
		DeviceName = deviceName;
		DeviceId = deviceId;
	}
}
