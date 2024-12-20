using System.Globalization;

namespace Exo.I2C;

public class DisplayDataChannelException : Exception
{
	private protected static string? GetErrorMessage(DisplayDataChannelError error)
		=> error switch
		{
			DisplayDataChannelError.InvalidResponseLength => "The received response has an invalid length.",
			DisplayDataChannelError.IncorrectResponseLength => "The received response has an incorrect length.",
			DisplayDataChannelError.InvalidChecksum => "The received response has an invalid DDC checksum.",
			DisplayDataChannelError.WrongDestinationAddress => "The received response has an unexpected destination address.",
			DisplayDataChannelError.WrongOpcode => "The received response is referencing the wrong DDC opcode.",
			DisplayDataChannelError.WrongVcpCode => "The received response does not match the requested VCP code.",
			DisplayDataChannelError.UnsupportedVcpCode => "The monitor rejected the request for the VCP code as unsupported. Some monitors can badly report VCP codes in the capabilities string.",
			DisplayDataChannelError.Error => "The monitor returned an unknown error for the VCP code.",
			DisplayDataChannelError.NonConsecutiveDataPackets => "Non consecutive data packets were received.",
			DisplayDataChannelError.MaximumDataLengthExceeded => "The data exceeded the maximum size.",
			_ => null
		};

	public DisplayDataChannelError Error { get; }

	public DisplayDataChannelException(DisplayDataChannelError error) : this(error, GetErrorMessage(error)) { }
	public DisplayDataChannelException(DisplayDataChannelError error, Exception? innerException) : this(error, GetErrorMessage(error), innerException) { }

	public DisplayDataChannelException(DisplayDataChannelError error, string? message) : base(message)
	{
		Error = error;
	}

	public DisplayDataChannelException(DisplayDataChannelError error, string? message, Exception? innerException) : base(message, innerException)
	{
		Error = error;
	}
}

public class DisplayDataChannelVcpException : DisplayDataChannelException
{
	private protected static string? GetErrorMessage(DisplayDataChannelError error, byte vcpCode)
		=> error switch
		{
			DisplayDataChannelError.UnsupportedVcpCode => string.Create(CultureInfo.InvariantCulture, $"The monitor rejected the request for VCP code {vcpCode:X2} as unsupported. Some monitors can badly report VCP codes in the capabilities string."),
			DisplayDataChannelError.Error => string.Create(CultureInfo.InvariantCulture, $"The monitor returned an unknown error for VCP code {vcpCode:X2}."),
			_ => GetErrorMessage(error)
		};

	public byte VcpCode { get; }
	public byte ReplyVcpCode { get; }

	public DisplayDataChannelVcpException(DisplayDataChannelError error, byte vcpCode) : this(error, vcpCode, vcpCode) { }
	public DisplayDataChannelVcpException(DisplayDataChannelError error, byte vcpCode, Exception? innerException) : this(error, vcpCode, vcpCode, innerException) { }

	public DisplayDataChannelVcpException(DisplayDataChannelError error, byte vcpCode, byte replyVcpCode)
		: this(error, vcpCode, replyVcpCode, GetErrorMessage(error, vcpCode)) { }
	public DisplayDataChannelVcpException(DisplayDataChannelError error, byte vcpCode, byte replyVcpCode, Exception? innerException)
		: this(error, vcpCode, replyVcpCode, GetErrorMessage(error, vcpCode), innerException) { }

	public DisplayDataChannelVcpException(DisplayDataChannelError error, byte vcpCode, string? message) : this(error, vcpCode, vcpCode, message) { }
	public DisplayDataChannelVcpException(DisplayDataChannelError error, byte vcpCode, string? message, Exception? innerException) : this(error, vcpCode, vcpCode, message, innerException) { }

	public DisplayDataChannelVcpException(DisplayDataChannelError error, byte vcpCode, byte replyVcpCode, string? message) : base(error, message)
	{
		VcpCode = vcpCode;
		ReplyVcpCode = replyVcpCode;
	}

	public DisplayDataChannelVcpException(DisplayDataChannelError error, byte vcpCode, byte replyVcpCode, string? message, Exception? innerException) : base(error, message, innerException)
	{
		VcpCode = vcpCode;
		ReplyVcpCode = replyVcpCode;
	}
}

public enum DisplayDataChannelError
{
	InvalidResponseLength = 1,
	IncorrectResponseLength = 2,
	InvalidChecksum = 3,
	WrongDestinationAddress = 4,
	WrongOpcode = 5,
	WrongVcpCode = 6,
	UnsupportedVcpCode = 7,
	Error = 8,
	NonConsecutiveDataPackets = 9,
	MaximumDataLengthExceeded = 10,
}
