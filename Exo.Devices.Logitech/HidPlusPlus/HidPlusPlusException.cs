using System.Runtime.Serialization;

namespace Exo.Devices.Logitech.HidPlusPlus;

public class HidPlusPlusException : Exception
{
	public HidPlusPlusErrorCode ErrorCode { get; }

	public HidPlusPlusException(HidPlusPlusErrorCode errorCode)
	{
		ErrorCode = errorCode;
	}

	public HidPlusPlusException(HidPlusPlusErrorCode errorCode, string? message) : base(message)
	{
		ErrorCode = errorCode;
	}

	public HidPlusPlusException(HidPlusPlusErrorCode errorCode, string? message, Exception? innerException) : base(message, innerException)
	{
		ErrorCode = errorCode;
	}

	protected HidPlusPlusException(HidPlusPlusErrorCode errorCode, SerializationInfo info, StreamingContext context) : base(info, context)
	{
		ErrorCode = errorCode;
	}
}
