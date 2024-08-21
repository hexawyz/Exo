using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class LockKeyFeatureHandler : FeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.LockKeyState;

			private byte _lockKeys;

			public LockKeys LockKeys => (LockKeys)_lockKeys;

			public LockKeyFeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
			}

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				var lockKeys = await Device
					.SendWithRetryAsync<LockKeyState.GetLockKeyStatus.Response>(FeatureIndex, LockKeyState.GetLockKeyStatus.FunctionId, retryCount, cancellationToken)
					.ConfigureAwait(false);

				ProcessStatusResponse(ref lockKeys);
			}

			protected override void HandleNotification(byte eventId, ReadOnlySpan<byte> response)
			{
				if (eventId != LockKeyState.GetLockKeyStatus.EventId) return;

				if (response.Length < 3) return;

				ProcessStatusResponse(ref Unsafe.As<byte, LockKeyState.GetLockKeyStatus.Response>(ref MemoryMarshal.GetReference(response)));
			}

			private void ProcessStatusResponse(ref LockKeyState.GetLockKeyStatus.Response response)
			{
				byte newLockKeys, oldLockKeys;

				newLockKeys = (byte)response.LockedKeys;

				lock (this)
				{
					oldLockKeys = _lockKeys;

					if (newLockKeys != oldLockKeys)
					{
						_lockKeys = newLockKeys;
					}
				}

				if (newLockKeys != oldLockKeys)
				{
					var device = Device;
					if (device.LockKeysChanged is { } lockKeysChanged)
					{
						_ = Task.Run(() => lockKeysChanged.Invoke(device, (LockKeys)newLockKeys));
					}
				}
			}
		}
	}
}
