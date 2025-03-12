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

public class MonitorControlCommunicationException : MonitorControlException
{
	public MonitorControlCommunicationException()
	{
	}

	public MonitorControlCommunicationException(string? message) : base(message)
	{
	}

	public MonitorControlCommunicationException(string? message, Exception? innerException) : base(message, innerException)
	{
	}
}

public class I2cNotSupportedException : MonitorControlCommunicationException
{
	public I2cNotSupportedException()
		: this("The monitor connected to the specified video output does not have an I2C bus.", null)
	{
	}

	public I2cNotSupportedException(string? message)
		: this(message, null)
	{
	}

	public I2cNotSupportedException(string? message, Exception? innerException) : base(message, innerException)
	{
		HResult = NativeMethods.ErrorGraphicsI2cNotSupported;
	}
}

public class I2cDeviceNotFoundException : MonitorControlCommunicationException
{
	public I2cDeviceNotFoundException()
		: this("No device on the I2C bus has the specified address.", null)
	{
	}

	public I2cDeviceNotFoundException(string? message)
		: this(message, null)
	{
	}

	public I2cDeviceNotFoundException(string? message, Exception? innerException) : base(message, innerException)
	{
		HResult = NativeMethods.ErrorGraphicsI2cNotSupported;
	}
}

public class I2cTransmissionException : MonitorControlCommunicationException
{
	public I2cTransmissionException()
		: this("An error occurred while transmitting data to the device on the I2C bus.", null)
	{
	}

	public I2cTransmissionException(string? message)
		: this(message, null)
	{
	}

	public I2cTransmissionException(string? message, Exception? innerException) : base(message, innerException)
	{
		HResult = NativeMethods.ErrorGraphicsI2cErrorTransmittingData;
	}
}

public class I2cReceptionException : MonitorControlCommunicationException
{
	public I2cReceptionException()
		: this("An error occurred while receiving data from the device on the I2C bus.", null)
	{
	}

	public I2cReceptionException(string? message)
		: this(message, null)
	{
	}

	public I2cReceptionException(string? message, Exception? innerException) : base(message, innerException)
	{
		HResult = NativeMethods.ErrorGraphicsI2cErrorReceivingData;
	}
}

public class InvalidDdcCiMessageCommandException : MonitorControlCommunicationException
{
	public InvalidDdcCiMessageCommandException()
		: this("An operation failed because a DDC/CI message had an invalid value in its command field.", null)
	{
	}

	public InvalidDdcCiMessageCommandException(string? message)
		: this(message, null)
	{
	}

	public InvalidDdcCiMessageCommandException(string? message, Exception? innerException) : base(message, innerException)
	{
		HResult = NativeMethods.ErrorGraphicsDdcCiInvalidMessageCommand;
	}
}

public class InvalidDdcCiMessageLengthException : MonitorControlCommunicationException
{
	public InvalidDdcCiMessageLengthException()
		: this("An error occurred because the field length of a DDC/CI message contained an invalid value.", null)
	{
	}

	public InvalidDdcCiMessageLengthException(string? message)
		: this(message, null)
	{
	}

	public InvalidDdcCiMessageLengthException(string? message, Exception? innerException) : base(message, innerException)
	{
		HResult = NativeMethods.ErrorGraphicsDdcCiInvalidMessageLength;
	}
}

public class InvalidDdcCiMessageChecksumException : MonitorControlCommunicationException
{
	public InvalidDdcCiMessageChecksumException()
		: this("An error occurred because the checksum field in a DDC/CI message did not match the message's computed checksum value. This error implies that the data was corrupted while it was being transmitted from a monitor to a computer.", null)
	{
	}

	public InvalidDdcCiMessageChecksumException(string? message)
		: this(message, null)
	{
	}

	public InvalidDdcCiMessageChecksumException(string? message, Exception? innerException) : base(message, innerException)
	{
		HResult = NativeMethods.ErrorGraphicsDdcCiInvalidMessageChecksum;
	}
}

public class MonitorNoLongerExistsException : MonitorControlCommunicationException
{
	public MonitorNoLongerExistsException()
		: this("The operating system asynchronously destroyed the monitor which corresponds to this handle because the operating system's state changed. This error typically occurs because the monitor PDO associated with this handle was removed, the monitor PDO associated with this handle was stopped, or a display mode change occurred. A display mode change occurs when windows sends a WM_DISPLAYCHANGE windows message to applications.", null)
	{
	}

	public MonitorNoLongerExistsException(string? message)
		: this(message, null)
	{
	}

	public MonitorNoLongerExistsException(string? message, Exception? innerException) : base(message, innerException)
	{
		HResult = NativeMethods.ErrorGraphicsDdcCiVcpNotSupported;
	}
}
