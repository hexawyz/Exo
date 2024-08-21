namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private abstract class BatteryFeatureHandler : FeatureHandler
		{
			public abstract BatteryPowerState PowerState { get; }

			protected BatteryFeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex) { }

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				await RefreshBatteryCapabilitiesAsync(retryCount, cancellationToken).ConfigureAwait(false);
				await RefreshBatteryStatusAsync(retryCount, cancellationToken).ConfigureAwait(false);
			}

			protected abstract Task RefreshBatteryCapabilitiesAsync(int retryCount, CancellationToken cancellationToken);
			protected abstract Task RefreshBatteryStatusAsync(int retryCount, CancellationToken cancellationToken);
		}
	}
}
