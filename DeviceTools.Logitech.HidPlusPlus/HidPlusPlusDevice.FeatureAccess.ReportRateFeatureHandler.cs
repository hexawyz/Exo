using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class ReportRateFeatureHandler : FeatureHandler
		{
			private ReportIntervals _supportedReportIntervals;
			private byte _reportInterval;

			public override HidPlusPlusFeature Feature => ReportRate.FeatureId;

			public ReportRateFeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
			}

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				_supportedReportIntervals =
				(
					await Device.SendWithRetryAsync<ReportRate.GetReportRateList.Response>(FeatureIndex, ReportRate.GetReportRateList.FunctionId, retryCount, cancellationToken).ConfigureAwait(false)
				).SupportedReportIntervals;

				_reportInterval =
				(
					await Device.SendWithRetryAsync<ReportRate.GetReportRate.Response>
					(
						FeatureIndex,
						ReportRate.GetReportRate.FunctionId,
						retryCount,
						cancellationToken
					).ConfigureAwait(false)
				).ReportIntervalInMilliseconds;
			}

			public ReportIntervals SupportedReportIntervals => _supportedReportIntervals;
			public byte ReportInterval => _reportInterval;

			public Task SetReportIntervalAsync(byte reportInterval, CancellationToken cancellationToken)
				=> Device.SendWithRetryAsync
				(
					FeatureIndex,
					ReportRate.SetReportRate.FunctionId,
					new ReportRate.SetReportRate.Request() { ReportIntervalInMilliseconds = reportInterval },
					HidPlusPlusTransportExtensions.DefaultRetryCount,
					cancellationToken
				);
		}
	}
}
