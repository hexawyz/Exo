using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class ColorLedEffectFeatureHandler : FeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.ColorLedEffects;

			public ColorLedEffectFeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
			}

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				var info = await Device.SendWithRetryAsync<ColorLedEffects.GetInfo.Response>(FeatureIndex, ColorLedEffects.GetInfo.FunctionId, retryCount, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
