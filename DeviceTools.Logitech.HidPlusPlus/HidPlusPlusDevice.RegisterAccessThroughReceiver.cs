using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Notifications;
using Microsoft.Extensions.Logging;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public class RegisterAccessThroughReceiver : RegisterAccess, IDeviceThroughReceiver
	{
		public new byte DeviceIndex => base.DeviceIndex;

		public event DeviceEventHandler? Connected;
		public event DeviceEventHandler? Disconnected;

		// 0 (default) if last published device state is disconnected; 1 if last published device state is connected.
		private bool _lastPublishedStateWasConnected;
		// Track the number of connect/disconnect events, and allow skipping .
		private int _version;

		protected RegisterAccessReceiver Receiver => Unsafe.As<RegisterAccessReceiver>(ParentOrTransport);
		protected sealed override HidPlusPlusTransport Transport => Receiver.Transport;

		internal RegisterAccessThroughReceiver(RegisterAccessReceiver parent, ILogger<RegisterAccessThroughReceiver> logger, ushort productId, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
			: base(parent, logger, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
			var device = Transport.Devices[deviceIndex];
			device.NotificationReceived += HandleNotification;
			Volatile.Write(ref device.CustomState, this);
			Receiver.OnDeviceDiscovered(this);
			if (deviceConnectionInfo.IsLinkEstablished)
			{
				Receiver.RegisterNotificationTask(HandleDeviceConnectionAsync(HidPlusPlusTransportExtensions.DefaultRetryCount, _version, default));
			}
		}

		private protected override void RaiseConnected(int version)
		{
			if (Volatile.Read(ref _version) == version)
			{
				if (!_lastPublishedStateWasConnected)
				{
					try
					{
						Receiver.RaiseDeviceConnected(this);
					}
					finally
					{
						try
						{
							Connected?.Invoke(this);
						}
						finally
						{
							_lastPublishedStateWasConnected = true;
						}
					}
				}
			}
		}

		private protected override void RaiseDisconnected(int version)
		{
			if (Volatile.Read(ref _version) == version)
			{
				if (_lastPublishedStateWasConnected)
				{
					try
					{
						Receiver.RaiseDeviceDisconnected(this);
					}
					finally
					{
						try
						{
							Disconnected?.Invoke(this);
						}
						finally
						{
							_lastPublishedStateWasConnected = false;
						}
					}
				}
			}
		}

		private void HandleNotification(ReadOnlySpan<byte> message)
		{
			if (message.Length < 7) return;

			var header = Unsafe.ReadUnaligned<RegisterAccessHeader>(ref MemoryMarshal.GetReference(message));

			// If we receive a device connect notification, it indicates a new device. (In the sense of not yet known; not necessarily a new pairing)
			// Once we create the device object, it will automatically process the notifications.
			if (header.SubId == SubId.DeviceConnect)
			{
				var parameters = Unsafe.ReadUnaligned<DeviceConnectionParameters>(ref Unsafe.AsRef(message[3]));

				// If the WPID has changed, unregister the current instance and forward the notification to the receiver. (It will recreate a new device)
				if (parameters.WirelessProductId != ProductId)
				{
					DisposeInternal();
					Receiver.HandleNotification(message);
					return;
				}

				bool wasConnected = DeviceConnectionInfo.IsLinkEstablished;
				bool isConnected = parameters.DeviceInfo.IsLinkEstablished;

				// Update the connection information.
				DeviceConnectionInfo = parameters.DeviceInfo;

				if (isConnected != wasConnected)
				{
					int version = Interlocked.Increment(ref _version);
					if (isConnected)
					{
						Receiver.RegisterNotificationTask(HandleDeviceConnectionAsync(HidPlusPlusTransportExtensions.DefaultRetryCount, version, default));
					}
					else
					{
						Receiver.OnDeviceDisconnected(this, version);
					}
				}
			}
			else
			{
				var connectionInfo = DeviceConnectionInfo;

				bool wasConnected = DeviceConnectionInfo.IsLinkEstablished;

				if (wasConnected)
				{
					connectionInfo.ConnectionFlags |= DeviceConnectionFlags.LinkNotEstablished;

					// Update the connection information.
					DeviceConnectionInfo = connectionInfo;

					int version = Interlocked.Increment(ref _version);
					Receiver.OnDeviceDisconnected(this, version);
				}
			}
		}

		private async Task HandleDeviceConnectionAsync(int retryCount, int version, CancellationToken cancellationToken)
		{
			await InitializeAsync(retryCount, cancellationToken).ConfigureAwait(false);

			Receiver.OnDeviceConnected(this, version);
		}

		private void DisposeInternal()
		{
			var device = Transport.Devices[DeviceIndex];
			device.NotificationReceived -= HandleNotification;
			Volatile.Write(ref device.CustomState, null);
		}

		public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
