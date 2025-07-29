using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class MultiPlatformFeatureHandler : FeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.MultiPlatform;

			public MultiPlatformFeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
			}

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				var infos = await Device.SendWithRetryAsync<MultiPlatform.GetFeatureInfos.Response>
				(
					FeatureIndex,
					MultiPlatform.GetFeatureInfos.FunctionId,
					retryCount,
					cancellationToken
				);
				for (int i = 0; i < infos.PlatformDescriptorCount; i++)
				{
					var platformDescriptor = await Device.SendWithRetryAsync<MultiPlatform.GetPlatformDescriptor.Request, MultiPlatform.GetPlatformDescriptor.Response>
					(
						FeatureIndex,
						MultiPlatform.GetPlatformDescriptor.FunctionId,
						new() { PlatformDescriptorIndex = (byte)i },
						retryCount,
						cancellationToken
					);
				}
				for (int i = 0; i < infos.HostCount; i++)
				{
					var hostPlatform = await Device.SendWithRetryAsync<MultiPlatform.GetHostPlatform.Request, MultiPlatform.GetHostPlatform.Response>
					(
						FeatureIndex,
						MultiPlatform.GetHostPlatform.FunctionId,
						new() { HostIndex = (byte)i },
						retryCount,
						cancellationToken
					);
				}
			}
		}
	}
}
