namespace Exo.Devices.NVidia;

internal sealed class NvApiException : Exception
{
	public NvApiError Status { get; }

	public NvApiException(NvApiError status, string? message) : base(message)
	{
		Status = status;
	}
}
