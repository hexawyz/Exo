using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class ReportRate
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.ReportRate;

	public static class GetReportRateList
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public ReportIntervals SupportedReportIntervals;
		}
	}

	public static class GetReportRate
	{
		public const byte FunctionId = 1;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public byte ReportIntervalInMilliseconds;
		}
	}

	public static class SetReportRate
	{
		public const byte FunctionId = 2;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public byte ReportIntervalInMilliseconds;
		}
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
