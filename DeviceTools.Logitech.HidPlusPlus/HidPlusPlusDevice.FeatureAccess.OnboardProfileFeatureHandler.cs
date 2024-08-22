using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class OnboardProfileFeatureHandler : FeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.OnboardProfiles;

			private OnBoardProfiles.GetInfo.Response _information;
			private DeviceMode _deviceMode;
			private bool _isDeviceSupported;

			public OnboardProfileFeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
			}

			public bool IsSupported => _isDeviceSupported;

			public DeviceMode DeviceMode => _deviceMode;

			private void EnsureSupport()
			{
				if (!IsSupported) throw new InvalidOperationException("The device is currently unsupported.");
			}

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				_information = await Device.SendWithRetryAsync<OnBoardProfiles.GetInfo.Response>(FeatureIndex, OnBoardProfiles.GetInfo.FunctionId, retryCount, cancellationToken).ConfigureAwait(false);

				// Same as in libratbag, this will prevent messing up with unsupported devices.
				_isDeviceSupported = _information.MemoryType is OnBoardProfiles.MemoryType.G402 &&
					_information.ProfileFormat is OnBoardProfiles.ProfileFormat.G402 or OnBoardProfiles.ProfileFormat.G303 or OnBoardProfiles.ProfileFormat.G900 or OnBoardProfiles.ProfileFormat.G915 &&
					_information.MacroFormat is OnBoardProfiles.MacroFormat.G402;

				if (!IsSupported) return;

				_deviceMode = (
					await Device.SendWithRetryAsync<OnBoardProfiles.GetDeviceMode.Response>
					(
						FeatureIndex,
						OnBoardProfiles.GetDeviceMode.FunctionId,
						retryCount,
						cancellationToken
					).ConfigureAwait(false)
				).Mode;
			}

			public Task SwitchToHostMode(CancellationToken cancellationToken)
				=> SwitchToMode(DeviceMode.Host, cancellationToken);

			public Task SwitchToOnBoardMode(CancellationToken cancellationToken)
				=> SwitchToMode(DeviceMode.OnBoardMemory, cancellationToken);

			private async Task SwitchToMode(DeviceMode mode, CancellationToken cancellationToken)
			{
				EnsureSupport();
				await Device.SendAsync(FeatureIndex, OnBoardProfiles.SetDeviceMode.FunctionId, new OnBoardProfiles.SetDeviceMode.Request { Mode = DeviceMode.Host }, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
