using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
internal readonly struct RawMessageHeader
{
	public readonly byte ReportId;
	public readonly byte DeviceIndex;
	public readonly byte SubIdOrFeatureIndex;
	public readonly byte AddressOrFunctionIdAndSoftwareId;

	public RawMessageHeader(byte reportId, byte deviceIndex, byte subIdOrFeatureIndex, byte addressOrFunctionIdAndSoftwareId)
	{
		ReportId = reportId;
		DeviceIndex = deviceIndex;
		SubIdOrFeatureIndex = subIdOrFeatureIndex;
		AddressOrFunctionIdAndSoftwareId = addressOrFunctionIdAndSoftwareId;
	}

	public bool EqualsWithoutReportId(RawMessageHeader other)
		=> DeviceIndex == other.DeviceIndex &&
			SubIdOrFeatureIndex == other.SubIdOrFeatureIndex &&
			AddressOrFunctionIdAndSoftwareId == other.AddressOrFunctionIdAndSoftwareId;
}
