using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Notifications;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public class FeatureAccessThroughReceiver : FeatureAccess, IDeviceThroughReceiver
	{
		public new byte DeviceIndex => base.DeviceIndex;

		public event DeviceEventHandler? Connected;
		public event DeviceEventHandler? Disconnected;

		protected RegisterAccessReceiver Receiver => Unsafe.As<RegisterAccessReceiver>(ParentOrTransport);
		protected sealed override HidPlusPlusTransport Transport => Receiver.Transport;

		private bool _isInfoInitialized;

		// 0 (default) if last published device state is disconnected; 1 if last published device state is connected.
		private bool _lastPublishedStateWasConnected;
		// Track the number of connect/disconnect events, and allow skipping .
		private int _version;

		internal FeatureAccessThroughReceiver
		(
			RegisterAccessReceiver parent,
			ushort productId,
			byte deviceIndex,
			DeviceConnectionInfo deviceConnectionInfo,
			FeatureAccessProtocol.DeviceType deviceType,
			ReadOnlyDictionary<HidPlusPlusFeature, byte>? cachedFeatures,
			string? friendlyName,
			string? serialNumber
		)
			: base(parent, productId, deviceIndex, deviceConnectionInfo, deviceType, cachedFeatures, friendlyName, serialNumber)
		{
			// If the device is connected and we reach this point in the code, relevant information will already have been fetched and passed through the parameters.
			_isInfoInitialized = deviceConnectionInfo.IsLinkEstablished;
			var device = Transport.Devices[deviceIndex];
			Volatile.Write(ref device.CustomState, this);
			Receiver.OnDeviceDiscovered(this);
			if (deviceConnectionInfo.IsLinkEstablished)
			{
				Receiver.RegisterNotificationTask(HandleDeviceConnectionAsync(HidPlusPlusTransportExtensions.DefaultRetryCount, _version, true, default));
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

		protected override void HandleNotification(ReadOnlySpan<byte> message)
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
					DisposeInternal(true);
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
						Receiver.RegisterNotificationTask(HandleDeviceConnectionAsync(HidPlusPlusTransportExtensions.DefaultRetryCount, version, Volatile.Read(ref _isInfoInitialized), default));
					}
					else
					{
						Receiver.OnDeviceDisconnected(this, version);
					}
				}
			}
			else if (header.SubId == SubId.DeviceDisconnect)
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
			else
			{
				base.HandleNotification(message);
			}
		}

		private async Task HandleDeviceConnectionAsync(int retryCount, int version, bool wasInfoInitialized, CancellationToken cancellationToken)
		{
			var transport = Transport;

			if (!wasInfoInitialized)
			{
				var features = await GetFeaturesWithRetryAsync(retryCount, cancellationToken).ConfigureAwait(false);

				var (retrievedType, retrievedName) = await FeatureAccessGetDeviceNameAndTypeAsync(transport, features, DeviceIndex, retryCount, cancellationToken).ConfigureAwait(false);

				// Update the device information if we were able to retrieve the device name.
				if (retrievedName is not null)
				{
					DeviceType = retrievedType;
					FriendlyName = retrievedName;
				}

				Volatile.Write(ref _isInfoInitialized, true);
			}

			await InitializeAsync(retryCount, cancellationToken).ConfigureAwait(false);

			Receiver.OnDeviceConnected(this, version);
		}

		// Instances of this class should not be disposed externally. The DisposeAsync method does nothing.
		public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
