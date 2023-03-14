using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract class HidPlusPlusException : Exception
{
	public byte ErrorCode { get; }

	private protected HidPlusPlusException(byte errorCode)
	{
		ErrorCode = errorCode;
	}

	private protected HidPlusPlusException(byte errorCode, string? message) : base(message)
	{
		ErrorCode = errorCode;
	}

	private protected HidPlusPlusException(byte errorCode, string? message, Exception? innerException) : base(message, innerException)
	{
		ErrorCode = errorCode;
	}

	private protected HidPlusPlusException(SerializationInfo info, StreamingContext context) : base(info, context)
	{
		ErrorCode = info.GetByte("HidppErrorCode");
	}

	public override void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		base.GetObjectData(info, context);
		info.AddValue("HidppErrorCode", ErrorCode);
	}
}

public abstract class HidPlusPlusException<TErrorCode> : HidPlusPlusException
	where TErrorCode : struct, Enum
{
	public new TErrorCode ErrorCode
	{
		get
		{
			var errorCode = base.ErrorCode;
			return Unsafe.As<byte, TErrorCode>(ref errorCode);
		}
	}

	private protected HidPlusPlusException(TErrorCode errorCode)
		: base(Unsafe.As<TErrorCode, byte>(ref errorCode), $"An exception occurred on the HID++ when processing the request: {errorCode}.")
	{
	}

	private protected HidPlusPlusException(TErrorCode errorCode, string? message) : base(Unsafe.As<TErrorCode, byte>(ref errorCode), message)
	{
	}

	private protected HidPlusPlusException(TErrorCode errorCode, string? message, Exception? innerException) : base(Unsafe.As<TErrorCode, byte>(ref errorCode), message, innerException)
	{
	}

	private protected HidPlusPlusException(SerializationInfo info, StreamingContext context) : base(info, context)
	{
	}
}

public sealed class HidPlusPlus1Exception : HidPlusPlusException<RegisterAccessProtocol.ErrorCode>
{
	public HidPlusPlus1Exception(RegisterAccessProtocol.ErrorCode errorCode): base(errorCode)
	{
	}

	public HidPlusPlus1Exception(RegisterAccessProtocol.ErrorCode errorCode, string? message) : base(errorCode, message)
	{
	}

	public HidPlusPlus1Exception(RegisterAccessProtocol.ErrorCode errorCode, string? message, Exception? innerException) : base(errorCode, message, innerException)
	{
	}

	private HidPlusPlus1Exception(SerializationInfo info, StreamingContext context) : base(info, context)
	{
	}
}

public sealed class HidPlusPlus2Exception : HidPlusPlusException<FeatureAccessProtocol.ErrorCode>
{
	public HidPlusPlus2Exception(FeatureAccessProtocol.ErrorCode errorCode) : base(errorCode)
	{
	}

	public HidPlusPlus2Exception(FeatureAccessProtocol.ErrorCode errorCode, string? message) : base(errorCode, message)
	{
	}

	public HidPlusPlus2Exception(FeatureAccessProtocol.ErrorCode errorCode, string? message, Exception? innerException) : base(errorCode, message, innerException)
	{
	}

	private HidPlusPlus2Exception(SerializationInfo info, StreamingContext context) : base(info, context)
	{
	}
}
