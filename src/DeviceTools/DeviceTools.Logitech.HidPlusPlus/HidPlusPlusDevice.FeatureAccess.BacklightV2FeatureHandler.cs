using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class BacklightV2FeatureHandler : FeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.BacklightV2;

			private ushort _backlightLevelAndCount;

			public BacklightState BacklightState => GetBacklightState(Volatile.Read(ref _backlightLevelAndCount));

			public BacklightV2FeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
			}

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				var backlightInfo = await Device
					.SendWithRetryAsync<BacklightV2.GetBacklightInfo.Response>(FeatureIndex, BacklightV2.GetBacklightInfo.FunctionId, retryCount, cancellationToken)
					.ConfigureAwait(false);

				ProcessStatusResponse(ref backlightInfo);
			}

			protected override void HandleNotification(byte eventId, ReadOnlySpan<byte> response)
			{
				if (eventId != BacklightV2.GetBacklightInfo.EventId) return;

				if (response.Length < 3) return;

				if (response.Length < 16)
				{
					HandleShortNotification(response);
				}
				else
				{
					ProcessStatusResponse(ref Unsafe.As<byte, BacklightV2.GetBacklightInfo.Response>(ref MemoryMarshal.GetReference(response)));
				}
			}

			// Not sure this is needed, but messages for V1 of the feature seemed to fit in 3 bytes.
			private void HandleShortNotification(ReadOnlySpan<byte> response)
			{
				BacklightV2.GetBacklightInfo.Response backlightInfo = default;

				response.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.As<BacklightV2.GetBacklightInfo.Response, byte>(ref backlightInfo), 16));

				ProcessStatusResponse(ref backlightInfo);
			}

			private void ProcessStatusResponse(ref BacklightV2.GetBacklightInfo.Response response)
			{
				ushort oldBacklightLevelAndCount;
				ushort newBacklightLevelAndCount;

				newBacklightLevelAndCount = (ushort)(response.LevelCount << 8 | response.CurrentLevel);

				lock (this)
				{
					oldBacklightLevelAndCount = _backlightLevelAndCount;

					if (newBacklightLevelAndCount != oldBacklightLevelAndCount)
					{
						_backlightLevelAndCount = newBacklightLevelAndCount;
					}
				}

				if (newBacklightLevelAndCount != oldBacklightLevelAndCount)
				{
					var device = Device;
					if (device.BacklightStateChanged is { } backlightStateChanged)
					{
						_ = Task.Run(() => backlightStateChanged.Invoke(device, GetBacklightState(newBacklightLevelAndCount)));
					}
				}
			}

			private BacklightState GetBacklightState(ushort backlightLevelAndCount)
			{
				return new((byte)(backlightLevelAndCount & 0xFF), (byte)(backlightLevelAndCount >> 8));
			}
		}
	}
}
