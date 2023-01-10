using System.Runtime.Serialization;

namespace Exo.Devices.Logitech.HidPlusPlus;

public class HidPlusPlusException : Exception
{
	public byte ErrorCode { get; }

	public HidPlusPlusException(byte errorCode)
	{
		ErrorCode = errorCode;
	}

	public HidPlusPlusException(byte errorCode, string? message) : base(message)
	{
		ErrorCode = errorCode;
	}

	public HidPlusPlusException(byte errorCode, string? message, Exception? innerException) : base(message, innerException)
	{
		ErrorCode = errorCode;
	}

	protected HidPlusPlusException(byte errorCode, SerializationInfo info, StreamingContext context) : base(info, context)
	{
		ErrorCode = errorCode;
	}
}
