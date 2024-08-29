using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Notifications;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Registers;
using Microsoft.Extensions.Logging;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public class RegisterAccessReceiver : RegisterAccess, IUsbReceiver
	{
		private LightweightSingleProducerSingleConsumerQueue<Task> _deviceOperationTaskQueue;
		private LightweightSingleProducerSingleConsumerQueue<(DeviceEventKind Kind, HidPlusPlusDevice Device, int Version)> _eventQueue;
		private readonly ILoggerFactory _loggerFactory;
		private readonly Task _deviceWatcherTask;
		private readonly Task _eventProcessingTask;
		private bool _deviceWatchStarted;

		public event ReceiverDeviceEventHandler? DeviceDiscovered;
		public event ReceiverDeviceEventHandler? DeviceConnected;
		public event ReceiverDeviceEventHandler? DeviceDisconnected;

		internal RegisterAccessReceiver(HidPlusPlusTransport transport, ILoggerFactory loggerFactory, ILogger<RegisterAccessReceiver> logger, HidPlusPlusDeviceId[] deviceIds, byte mainDeviceIdIndex, byte deviceIndex, DeviceConnectionInfo deviceConnectionInfo, string? friendlyName, string? serialNumber)
			: base(transport, logger, deviceIds, mainDeviceIdIndex, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
			_deviceOperationTaskQueue = new();
			_eventQueue = new();
			_loggerFactory = loggerFactory;
			transport.NotificationReceived += HandleNotification;
			_deviceWatcherTask = WatchDevicesAsync();
			_eventProcessingTask = ProcessEventsAsync();
		}

		// NB: We don't need to unregister our notification handler here, since the transport is owned by the current instance.
		public override async ValueTask DisposeAsync(bool parentDisposed)
		{
			await Transport.DisposeAsync().ConfigureAwait(false);
			_deviceOperationTaskQueue.Dispose();
			_eventQueue.Dispose();
			foreach (var deviceState in Transport.Devices)
			{
				if (deviceState.IsDefault) continue;
				var state = deviceState.CustomState;
				if (state is not HidPlusPlusDevice device)
				{
					if (state is not Task<HidPlusPlusDevice> task) continue;
					try
					{
						device = await task.ConfigureAwait(false);
					}
					catch
					{
						continue;
					}
				}
				try
				{
					await device.DisposeAsync().ConfigureAwait(false);
				}
				catch
				{
				}
			}
			await _deviceWatcherTask.ConfigureAwait(false);
			await _eventProcessingTask.ConfigureAwait(false);
		}

		private async Task WatchDevicesAsync()
		{
			while (true)
			{
				Task task;

				try
				{
					task = await _deviceOperationTaskQueue.DequeueAsync().ConfigureAwait(false);
				}
				catch (ObjectDisposedException)
				{
					return;
				}

				try
				{
					await task.ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Logger.RegisterAccessReceiverDeviceUnhandledException(ex);
				}
			}
		}

		private async Task ProcessEventsAsync()
		{
			while (true)
			{
				DeviceEventKind kind;
				HidPlusPlusDevice device;
				int version;
				try
				{
					(kind, device, version) = await _eventQueue.DequeueAsync().ConfigureAwait(false);
				}
				catch (ObjectDisposedException)
				{
					return;
				}

				try
				{
					switch (kind)
					{
					case DeviceEventKind.DeviceDiscovered:
						DeviceDiscovered?.Invoke(this, device);
						break;
					case DeviceEventKind.DeviceConnected:
						device.RaiseConnected(version);
						break;
					case DeviceEventKind.DeviceDisconnected:
						device.RaiseDisconnected(version);
						break;
					}
				}
				catch (Exception ex)
				{
					Logger.RegisterAccessReceiverDeviceEventHandlerException(kind, ex);
				}
			}
		}

		/// <summary>Gets the devices currently known.</summary>
		/// <remarks>
		/// <para>
		/// This method is executed concurrently with device discovery. It is not guaranteed to return a strictly consistent result.
		/// The list of devices will, however, not change frequently after the initial discovery.
		/// Only device pairing or unpairing can affect the observed list of devices.
		/// </para>
		/// <para>
		/// In order to properly track devices, it is recommended to track devices by first setting up the events <see cref="DeviceDiscovered"/>,
		/// <see cref="DeviceConnected"/> and <see cref="DeviceDisconnected"/> before calling <see cref="StartWatchingDevicesAsync(CancellationToken)"/>.
		/// </para>
		/// <para>Before <see cref="StartWatchingDevicesAsync(CancellationToken)"/> is called, no child device will be registered.</para>
		/// </remarks>
		public async Task<HidPlusPlusDevice[]> GetCurrentDevicesAsync()
		{
			var devices = new List<HidPlusPlusDevice>();

			foreach (var state in Transport.Devices)
			{
				switch (state.CustomState)
				{
				case HidPlusPlusDevice device: devices.Add(device); break;
				case Task<HidPlusPlusDevice> task:
					try
					{
						devices.Add(await task.ConfigureAwait(false));
					}
					catch { }
					break;
				}
			}

			return devices.ToArray();
		}

		public Task StartWatchingDevicesAsync(CancellationToken cancellationToken)
			=> StartWatchingDevicesAsync(HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public async Task StartWatchingDevicesAsync(int retryCount, CancellationToken cancellationToken)
		{
			await EnableDeviceNotificationsAsync(retryCount, cancellationToken).ConfigureAwait(false);
			Volatile.Write(ref _deviceWatchStarted, true);
			await EnumerateDevicesAsync(retryCount, cancellationToken).ConfigureAwait(false);
		}

		internal async Task EnableDeviceNotificationsAsync(int retryCount, CancellationToken cancellationToken)
		{
			// Enable device arrival notifications, and set the "software present" flag.
			await Transport.RegisterAccessSetRegisterWithRetryAsync
			(
				255,
				Address.EnableHidPlusPlusNotifications,
				new EnableHidPlusPlusNotifications.Parameters
				{
					ReceiverReportingFlags = ReceiverReportingFlags.WirelessNotifications | ReceiverReportingFlags.SoftwarePresent
				},
				retryCount,
				cancellationToken
			).ConfigureAwait(false);
		}

		internal async Task EnumerateDevicesAsync(int retryCount, CancellationToken cancellationToken)
		{
			// Enumerate all connected devices.
			await Transport.RegisterAccessSetRegisterWithRetryAsync
			(
				255,
				Address.ConnectionState,
				new ConnectionState.SetRequest
				{
					Action = ConnectionStateAction.FakeDeviceArrival
				},
				retryCount,
				cancellationToken
			).ConfigureAwait(false);
		}

		protected sealed override HidPlusPlusTransport Transport => Unsafe.As<HidPlusPlusTransport>(ParentOrTransport);

		protected virtual DeviceIdSource DefaultChildDeviceIdSource => DeviceIdSource.EQuad;

		private protected sealed override void OnDeviceDiscovered(HidPlusPlusDevice device) => _eventQueue.Enqueue((DeviceEventKind.DeviceDiscovered, device, 0));
		private protected sealed override void OnDeviceConnected(HidPlusPlusDevice device, int version) => _eventQueue.Enqueue((DeviceEventKind.DeviceConnected, device, version));
		private protected sealed override void OnDeviceDisconnected(HidPlusPlusDevice device, int version) => _eventQueue.Enqueue((DeviceEventKind.DeviceDisconnected, device, version));

		private protected override void RaiseDeviceConnected(HidPlusPlusDevice device) => DeviceConnected?.Invoke(this, device);
		private protected override void RaiseDeviceDisconnected(HidPlusPlusDevice device) => DeviceDisconnected?.Invoke(this, device);

		private protected virtual HidPlusPlusProtocolFlavor InferProtocolFlavor(in DeviceConnectionParameters deviceConnectionParameters)
			=> TryInferProductCategory(deviceConnectionParameters.WirelessProductId, out var productCategory) && productCategory == ProductCategory.QuadFapDevice ?
				HidPlusPlusProtocolFlavor.FeatureAccessOverRegisterAccess :
				HidPlusPlusProtocolFlavor.RegisterAccess;

		// Child devices can forward notifications here when necessary. e.g. When the WPID of a device has changed.
		internal void HandleNotification(ReadOnlySpan<byte> message)
		{
			if (message.Length < 7) return;

			var header = Unsafe.ReadUnaligned<RegisterAccessHeader>(ref MemoryMarshal.GetReference(message));

			// If we receive a device connect notification, it indicates a new device. (In the sense of not yet known; not necessarily a new pairing)
			// Once we create the device object, it will automatically process the notifications.
			if (header.SubId == SubId.DeviceConnect)
			{
				// Ignore device connect notifications if the StartWatchingDevicesAsync method ahs not been called.
				// This ensures that consumers of the class have a chance to observe all devices through events.
				if (!Volatile.Read(ref _deviceWatchStarted)) return;

				var parameters = Unsafe.ReadUnaligned<DeviceConnectionParameters>(ref Unsafe.AsRef(message[3]));

				// All (Quad) HID++ 2.0 should adhere to this product ID mapping for now. It is important to know this in advance because the device might be offline.
				var protocolFlavor = InferProtocolFlavor(parameters);

				RegisterNotificationTask(ProcessDeviceArrivalAsync(parameters.WirelessProductId, header.DeviceId, parameters.DeviceInfo, protocolFlavor));
			}
		}

		// Get device information from the pairing data in the receiver.
		// For HID++ 2.0 devices, this may be less good than what will be discovered through the device itself, but this will work even when the device is disconnected.
		protected virtual async Task<(DeviceType DeviceType, string? DeviceName, string? SerialNumber)> GetPairedDeviceInformationAsync
		(
			byte deviceIndex,
			ushort productId,
			int retryCount,
			CancellationToken cancellationToken
		)
		{
			if (deviceIndex is 0x00 or > 0x0F) return default;

			DeviceType deviceType = DeviceType.Unknown;
			string? deviceName = null;
			string? serialNumber = null;

			try
			{
				var pairingInformationResponse = await Transport.RegisterAccessGetRegisterWithOneExtraParameterWithRetryAsync<NonVolatileAndPairingInformation.Request, NonVolatileAndPairingInformation.PairingInformationResponse>
				(
					255,
					Address.NonVolatileAndPairingInformation,
					new(NonVolatileAndPairingInformation.Parameter.PairingInformation1 + (deviceIndex - 1)),
					retryCount,
					cancellationToken
				);

				deviceType = (DeviceType)pairingInformationResponse.DeviceType;
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == ErrorCode.InvalidParameter)
			{
			}

			try
			{
				var extendedPairingInformationResponse = await Transport.RegisterAccessGetRegisterWithOneExtraParameterWithRetryAsync<NonVolatileAndPairingInformation.Request, NonVolatileAndPairingInformation.ExtendedPairingInformationResponse>
				(
					255,
					Address.NonVolatileAndPairingInformation,
					new(NonVolatileAndPairingInformation.Parameter.ExtendedPairingInformation1 + (deviceIndex - 1)),
					retryCount,
					cancellationToken
				);

				serialNumber = FormatRegisterAccessSerialNumber(productId, extendedPairingInformationResponse.SerialNumber);
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == ErrorCode.InvalidParameter)
			{
			}

			try
			{
				var deviceNameResponse = await Transport.RegisterAccessGetRegisterWithOneExtraParameterWithRetryAsync<NonVolatileAndPairingInformation.Request, NonVolatileAndPairingInformation.DeviceNameResponse>
				(
					255,
					Address.NonVolatileAndPairingInformation,
					new(NonVolatileAndPairingInformation.Parameter.DeviceName1 + (deviceIndex - 1)),
					retryCount,
					cancellationToken
				);

				deviceName = deviceNameResponse.GetDeviceName();
			}
			catch (HidPlusPlus1Exception ex) when (ex.ErrorCode == ErrorCode.InvalidParameter)
			{
			}

			return (deviceType, deviceName, serialNumber);
		}

		// To call in order to register a task started within a notification handler.
		// It should *only* be called within a notification handler, as notifications are processed sequentially and will not generate race conditions here.
		// The tasks registered here will be tracked internally for completion.
		internal void RegisterNotificationTask(Task task) => _deviceOperationTaskQueue.Enqueue(task);

		// This method can never finish synchronously because it will wait on message processing. (This is kinda important for the correct order of state updates)
		private async Task ProcessDeviceArrivalAsync(ushort productId, byte deviceIndex, DeviceConnectionInfo deviceInfo, HidPlusPlusProtocolFlavor protocolFlavor)
		{
			// Don't (try to) create the same device object twice. The custom state acts as some kind of lock, while also storing the associated device object.
			if (Volatile.Read(ref Transport.Devices[deviceIndex].CustomState) is null)
			{
				try
				{
					var task = CreateConnectedDeviceAsync(protocolFlavor, productId, deviceIndex, deviceInfo, HidPlusPlusTransportExtensions.DefaultRetryCount, default);

					// If the device is created quickly enough (relatively unlikely but hey…), the state could already have been updated with the device object.
					Interlocked.CompareExchange(ref Transport.Devices[deviceIndex].CustomState, task, null);

					await task.ConfigureAwait(false);
				}
				catch
				{
					Volatile.Write(ref Transport.Devices[deviceIndex].CustomState, null);
					throw;
				}
			}
		}

		private async Task<HidPlusPlusDevice> CreateConnectedDeviceAsync
		(
			HidPlusPlusProtocolFlavor protocolFlavor,
			ushort productId,
			byte deviceIndex,
			DeviceConnectionInfo deviceInfo,
			int retryCount,
			CancellationToken cancellationToken
		)
		{
			var (_, deviceName, serialNumber) = await GetPairedDeviceInformationAsync(deviceIndex, productId, retryCount, cancellationToken).ConfigureAwait(false);
			return await CreateAsync(this, Transport, _loggerFactory, protocolFlavor, new(DefaultChildDeviceIdSource, productId), deviceIndex, deviceInfo, deviceName, serialNumber, retryCount, default).ConfigureAwait(false);
		}
	}
}
