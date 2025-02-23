using Microsoft.Extensions.Logging;
using static DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features.KeyboardReprogrammableKeysAndMouseButtonsV5;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class KeyboardReprogrammableKeysAndMouseButtonsV5FeatureHandler : FeatureHandler
		{
			private readonly byte _featureVersion;

			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.KeyboardReprogrammableKeysAndMouseButtonsV5;

			public byte FeatureVersion => _featureVersion;

			public KeyboardReprogrammableKeysAndMouseButtonsV5FeatureHandler(FeatureAccess device, byte featureIndex, byte featureVersion) : base(device, featureIndex)
			{
				_featureVersion = featureVersion;
			}

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				var infos = await GetControlInformationsAsync(retryCount, cancellationToken).ConfigureAwait(false);
			}

			private async ValueTask<GetControlInfo.Response[]> GetControlInformationsAsync(int retryCount, CancellationToken cancellationToken)
			{
				var countResponse = await Device.SendWithRetryAsync<GetCount.Response>(FeatureIndex, GetCount.FunctionId, retryCount, cancellationToken).ConfigureAwait(false);

				if (countResponse.Count == 0) return [];

				return await GetControlInformationsAsync(countResponse.Count, retryCount, cancellationToken).ConfigureAwait(false);
			}

			private async ValueTask<GetControlInfo.Response[]> GetControlInformationsAsync(nuint count, int retryCount, CancellationToken cancellationToken)
			{
				var infos = new GetControlInfo.Response[count];
				GetControlInfo.Request request = new();
				for (nuint i = 0; i < count; i++)
				{
					request.Index = (byte)i;
					infos[i] = await Device.SendWithRetryAsync<GetControlInfo.Request, GetControlInfo.Response>
					(
						FeatureIndex,
						GetControlInfo.FunctionId,
						in request,
						retryCount,
						cancellationToken
					).ConfigureAwait(false);
				}
				if (Device.Logger.IsEnabled(LogLevel.Debug))
				{
					LogControlInformations(count, infos);
				}
				return infos;
			}

			private void LogControlInformations(nuint count, GetControlInfo.Response[] infos)
			{
				for (nuint i = 0; i < count; i++)
				{
					ref readonly var info = ref infos[i];
					// Basically produce nicer-looking output if some details are unnecessary.
					// NB: It is possible to have ControlFlags.FunctionKey with Position 0 (That should indicate FN-lock, from what I understand. Don't know if it can be something else.)
					if (info.Position == 0)
					{
						if (info.GroupNumber == 0 && info.GroupMask == 0)
						{
							Device.Logger.FeatureAccessDevice1B04NonRemappableControlId(Device.SerialNumber, info.ControlId, info.TaskId, info.Flags, info.ReportingCapabilities);
						}
						else
						{
							Device.Logger.FeatureAccessDevice1B04ControlIdWithGroup(Device.SerialNumber, info.ControlId, info.TaskId, info.Flags, info.GroupNumber, info.GroupMask, info.ReportingCapabilities);
						}
					}
					else
					{
						if (info.GroupNumber == 0 && info.GroupMask == 0)
						{
							Device.Logger.FeatureAccessDevice1B04NonRemappableControlIdWithPosition(Device.SerialNumber, info.ControlId, info.TaskId, info.Flags, info.Position, info.ReportingCapabilities);
						}
						else
						{
							Device.Logger.FeatureAccessDevice1B04ControlIdWithPositionAndGroup(Device.SerialNumber, info.ControlId, info.TaskId, info.Flags, info.Position, info.GroupNumber, info.GroupMask, info.ReportingCapabilities);
						}
					}
				}
			}
		}
	}
}
