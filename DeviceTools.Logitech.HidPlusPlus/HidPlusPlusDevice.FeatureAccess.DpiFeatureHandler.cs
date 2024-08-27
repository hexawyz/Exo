using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class DpiFeatureHandler : FeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.AdjustableDpi;

			private byte _sensorCount;

			public DpiFeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
			}

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				byte sensorCount =
				(
					await Device.SendWithRetryAsync<AdjustableDpi.GetSensorCount.Response>(FeatureIndex, AdjustableDpi.GetSensorCount.FunctionId, retryCount, cancellationToken)
						.ConfigureAwait(false)
				).SensorCount;

				// Can't make any sense of these informations. Usefulness of this feature may depend on the model, but everything reported here seems relatively inaccurate.
				// The DPI list seemed to include the max DPI supported by the mouse, which is good, but the current DPI info seems to only be valid in host mode.
				// Other infos were pure rubbish ?
				for (int i = 0; i < sensorCount; i++)
				{
					var dpiInformation = await Device.SendWithRetryAsync<AdjustableDpi.GetSensorDpi.Request, AdjustableDpi.GetSensorDpi.Response>
					(
						FeatureIndex,
						AdjustableDpi.GetSensorDpi.FunctionId,
						new() { SensorIndex = (byte)i },
						retryCount,
						cancellationToken
					).ConfigureAwait(false);

					var dpiList = await Device.SendWithRetryAsync<AdjustableDpi.GetSensorDpiList.Request, AdjustableDpi.GetSensorDpiList.Response>
					(
						FeatureIndex,
						AdjustableDpi.GetSensorDpiList.FunctionId,
						new() { SensorIndex = (byte)i },
						retryCount,
						cancellationToken
					).ConfigureAwait(false);
				}

				_sensorCount = sensorCount;
			}
		}
	}
}
