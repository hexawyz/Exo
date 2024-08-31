namespace DeviceTools.DisplayDevices;

public class MonitorControlException : Exception
{
	public MonitorControlException()
	{
	}

	public MonitorControlException(string? message) : base(message)
	{
	}

	public MonitorControlException(string? message, Exception? innerException) : base(message, innerException)
	{
	}
}

public class VcpCodeNotSupportedException : MonitorControlException
{
	public VcpCodeNotSupportedException()
		: this("The monitor does not support the specified VCP code.", null)
	{
	}

	public VcpCodeNotSupportedException(string? message)
		: this(message, null)
	{
	}

	public VcpCodeNotSupportedException(string? message, Exception? innerException) : base(message, innerException)
	{
		HResult = NativeMethods.ErrorGraphicsDdcCiVcpNotSupported;
	}
}
