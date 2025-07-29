using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class DisabledKeysFeatureHandler : FeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.DisableKeys;

			public DisabledKeysFeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
			}

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				var infos = await Device.SendWithRetryAsync<DisableKeys.GetCapabilities.Response>
				(
					FeatureIndex,
					DisableKeys.GetCapabilities.FunctionId,
					retryCount,
					cancellationToken
				);
				Device.Logger.FeatureAccessDevice4521AvailableKeys(Device.SerialNumber, infos.AvailableKeys);
			}
		}
	}
}
