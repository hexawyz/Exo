namespace Exo.Devices.NVidia;

internal sealed class NvApiException : Exception
{
	public uint Status { get; }

	public NvApiException(uint status, string? message) : base(message)
	{
		Status = status;
	}
}
