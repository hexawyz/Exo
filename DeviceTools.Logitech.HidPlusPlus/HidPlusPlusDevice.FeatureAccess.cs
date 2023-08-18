using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract class FeatureAccess : HidPlusPlusDevice
	{
		private abstract class BatteryState : NotificationHandler
		{
			protected FeatureAccess Device { get; }

			public abstract BatteryPowerState PowerState { get; }

			protected BatteryState(FeatureAccess device) => Device = device;

			public abstract Task RefreshBatteryCapabilitiesAsync(int retryCount, CancellationToken cancellationToken);
			public abstract Task RefreshBatteryStatusAsync(int retryCount, CancellationToken cancellationToken);
		}

		private sealed class UnifiedBatteryState : BatteryState
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.UnifiedBattery;

			private readonly byte _featureIndex;

			private UnifiedBattery.BatteryLevels _supportedBatteryLevels;
			private UnifiedBattery.BatteryFlags _batteryFlags;

			private uint _batteryLevelAndStatus;

			public override BatteryPowerState PowerState => GetBatteryPowerState(Volatile.Read(ref _batteryLevelAndStatus));

			private BatteryPowerState GetBatteryPowerState(uint batteryLevelAndStatus)
				=> GetBatteryPowerState
				(
					(byte)batteryLevelAndStatus,
					(UnifiedBattery.BatteryLevels)(byte)(batteryLevelAndStatus >> 8),
					(UnifiedBattery.ChargingStatus)(byte)(batteryLevelAndStatus >> 16),
					((byte)(batteryLevelAndStatus >> 24) & 1) != 0
				);

			private BatteryPowerState GetBatteryPowerState
			(
				byte batteryLevelPercentage,
				UnifiedBattery.BatteryLevels batteryLevel,
				UnifiedBattery.ChargingStatus chargingStatus,
				bool isExternalPowerConnected
			)
			{
				byte rawBatteryLevel = 0;

				if ((_batteryFlags & UnifiedBattery.BatteryFlags.StateOfCharge) != 0)
				{
					rawBatteryLevel = batteryLevelPercentage;
				}
				else
				{
					if ((batteryLevel & UnifiedBattery.BatteryLevels.Full) != 0)
					{
						rawBatteryLevel = 100;
					}
					else if ((batteryLevel & UnifiedBattery.BatteryLevels.Good) != 0)
					{
						rawBatteryLevel = 60;
					}
					else if ((batteryLevel & UnifiedBattery.BatteryLevels.Low) != 0)
					{
						rawBatteryLevel = 20;
					}
					else if ((batteryLevel & UnifiedBattery.BatteryLevels.Critical) != 0)
					{
						rawBatteryLevel = 10;
					}
				}

				var chargeStatus = BatteryChargeStatus.Discharging;
				var externalPowerStatus = isExternalPowerConnected ? BatteryExternalPowerStatus.IsConnected : BatteryExternalPowerStatus.None;

				switch (chargingStatus)
				{
				case UnifiedBattery.ChargingStatus.Discharging:
					chargeStatus = BatteryChargeStatus.Discharging;
					break;
				case UnifiedBattery.ChargingStatus.Charging:
					chargeStatus = BatteryChargeStatus.Charging;
					break;
				case UnifiedBattery.ChargingStatus.SlowCharging:
					chargeStatus = BatteryChargeStatus.Charging; // TODO: Is it slow charging or close to completion ?
					externalPowerStatus |= BatteryExternalPowerStatus.IsChargingBelowOptimalSpeed;
					break;
				case UnifiedBattery.ChargingStatus.ChargingComplete:
					chargeStatus = BatteryChargeStatus.ChargingComplete;
					break;
				case UnifiedBattery.ChargingStatus.ChargingError:
					chargeStatus = BatteryChargeStatus.ChargingError;
					break;
				}

				return new(rawBatteryLevel, chargeStatus, externalPowerStatus);
			}

			public UnifiedBatteryState(FeatureAccess device, byte featureIndex) : base(device) => _featureIndex = featureIndex;

			public override async Task RefreshBatteryCapabilitiesAsync(int retryCount, CancellationToken cancellationToken)
			{
				var response = await Device.SendWithRetryAsync<UnifiedBattery.GetCapabilities.Response>
				(
					_featureIndex,
					UnifiedBattery.GetCapabilities.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				_supportedBatteryLevels = response.SupportedBatteryLevels;
				_batteryFlags = response.BatteryFlags;
			}

			public override async Task RefreshBatteryStatusAsync(int retryCount, CancellationToken cancellationToken)
			{
				var response = await Device.SendWithRetryAsync<UnifiedBattery.GetStatus.Response>
				(
					_featureIndex,
					UnifiedBattery.GetStatus.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				ProcessStatusResponse(ref response);
			}

			protected override void HandleNotification(byte eventId, ReadOnlySpan<byte> response)
			{
				if (response.Length < 16) return;

				ProcessStatusResponse(ref Unsafe.As<byte, UnifiedBattery.GetStatus.Response>(ref MemoryMarshal.GetReference(response)));
			}

			private void ProcessStatusResponse(ref UnifiedBattery.GetStatus.Response response)
			{
				uint oldBatteryLevelAndStatus;
				uint newBatteryLevelAndStatus;

				lock (this)
				{
					oldBatteryLevelAndStatus = _batteryLevelAndStatus;
					newBatteryLevelAndStatus = response.StateOfCharge | (uint)response.BatteryLevel << 8 | (uint)response.ChargingStatus << 16 | (response.HasExternalPower ? 1U << 24 : 0);

					if (newBatteryLevelAndStatus != oldBatteryLevelAndStatus)
					{
						Volatile.Write(ref _batteryLevelAndStatus, newBatteryLevelAndStatus);
					}
				}

				if (newBatteryLevelAndStatus != oldBatteryLevelAndStatus)
				{
					var device = Device;
					if (Device.BatteryChargeStateChanged is { } batteryChargeStateChanged)
					{
						_ = Task.Run(() => batteryChargeStateChanged.Invoke(device, GetBatteryPowerState(newBatteryLevelAndStatus)));
					}
				}
			}
		}

		private sealed class LegacyBatteryState : BatteryState
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.BatteryUnifiedLevelStatus;

			private readonly byte _featureIndex;

			private byte _levelCount;
			private BatteryUnifiedLevelStatus.BatteryCapabilityFlags _capabilityFlags;

			private uint _batteryLevelAndStatus;

			public override BatteryPowerState PowerState => GetBatteryPowerState(Volatile.Read(ref _batteryLevelAndStatus));

			private static BatteryPowerState GetBatteryPowerState(uint batteryLevelAndStatus)
				=> GetBatteryPowerState((short)batteryLevelAndStatus, (BatteryUnifiedLevelStatus.BatteryStatus)(byte)(batteryLevelAndStatus >> 16));

			private static BatteryPowerState GetBatteryPowerState(short batteryLevel, BatteryUnifiedLevelStatus.BatteryStatus batteryStatus)
			{
				var chargeStatus = BatteryChargeStatus.Discharging;
				var externalPowerStatus = BatteryExternalPowerStatus.None;

				switch (batteryStatus)
				{
				case BatteryUnifiedLevelStatus.BatteryStatus.Discharging:
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.Recharging:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.Charging, BatteryExternalPowerStatus.IsConnected);
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.ChargeInFinalStage:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.ChargingNearlyComplete, BatteryExternalPowerStatus.IsConnected);
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.ChargeComplete:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.ChargingComplete, BatteryExternalPowerStatus.IsConnected);
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.RechargingBelowOptimalSpeed:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.Charging, BatteryExternalPowerStatus.IsConnected | BatteryExternalPowerStatus.IsChargingBelowOptimalSpeed);
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.InvalidBatteryType:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.InvalidBatteryType, BatteryExternalPowerStatus.None);
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.ThermalError:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.BatteryTooHot, BatteryExternalPowerStatus.None);
					break;
				case BatteryUnifiedLevelStatus.BatteryStatus.OtherChargingError:
					(chargeStatus, externalPowerStatus) = (BatteryChargeStatus.InvalidBatteryType, BatteryExternalPowerStatus.None);
					break;
				}

				return new((ushort)batteryLevel <= 255 ? (byte)batteryLevel : null, chargeStatus, externalPowerStatus);
			}

			public LegacyBatteryState(FeatureAccess device, byte featureIndex) : base(device)
			{
				_featureIndex = featureIndex;
				_batteryLevelAndStatus = 0xFFFF;
			}

			public override async Task RefreshBatteryCapabilitiesAsync(int retryCount, CancellationToken cancellationToken)
			{
				var response = await Device.SendWithRetryAsync<BatteryUnifiedLevelStatus.GetBatteryCapability.Response>
				(
					_featureIndex,
					BatteryUnifiedLevelStatus.GetBatteryCapability.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				_levelCount = response.NumberOfLevels;
				_capabilityFlags = response.Flags;
			}

			public override async Task RefreshBatteryStatusAsync(int retryCount, CancellationToken cancellationToken)
			{
				var response = await Device.SendWithRetryAsync<BatteryUnifiedLevelStatus.GetBatteryLevelStatus.Response>
				(
					_featureIndex,
					BatteryUnifiedLevelStatus.GetBatteryLevelStatus.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				ProcessStatusResponse(ref response);
			}

			protected override void HandleNotification(byte eventId, ReadOnlySpan<byte> response)
			{
				if (response.Length < 16) return;

				ProcessStatusResponse(ref Unsafe.As<byte, BatteryUnifiedLevelStatus.GetBatteryLevelStatus.Response>(ref MemoryMarshal.GetReference(response)));
			}

			private void ProcessStatusResponse(ref BatteryUnifiedLevelStatus.GetBatteryLevelStatus.Response response)
			{
				uint oldBatteryLevelAndStatus;
				uint newBatteryLevelAndStatus;

				lock (this)
				{
					oldBatteryLevelAndStatus = _batteryLevelAndStatus;
					short newBatteryLevel = response.BatteryDischargeLevel;

					// It seems that the charge level can be reported as zero when the device is charging. (Which explains the Windows 0% notification when starting the keyboard plugged)
					// We can try to rely on the battery status to provide a better approximate in some cases.
					if (response.BatteryStatus is BatteryUnifiedLevelStatus.BatteryStatus.ChargeComplete)
					{
						newBatteryLevel = 100;
					}
					else if (response.BatteryStatus is BatteryUnifiedLevelStatus.BatteryStatus.ChargeInFinalStage)
					{
						newBatteryLevel = 90;
					}
					else if (response.BatteryDischargeLevel == 0)
					{
						newBatteryLevel = -1;
					}

					newBatteryLevelAndStatus = (uint)response.BatteryStatus << 16 | (ushort)newBatteryLevel;

					if (newBatteryLevelAndStatus != oldBatteryLevelAndStatus)
					{
						Volatile.Write(ref _batteryLevelAndStatus, newBatteryLevelAndStatus);
					}
				}

				if (newBatteryLevelAndStatus != oldBatteryLevelAndStatus)
				{
					var device = Device;
					if (Device.BatteryChargeStateChanged is { } batteryChargeStateChanged)
					{
						_ = Task.Run(() => batteryChargeStateChanged.Invoke(device, GetBatteryPowerState(newBatteryLevelAndStatus)));
					}
				}
			}
		}

		// Fields are not readonly because devices seen through a receiver can be discovered while disconnected.
		// The values should be updated when the device is connected.
		private protected ReadOnlyDictionary<HidPlusPlusFeature, byte>? CachedFeatures;
		// An array of notification handlers, with one slot for each feature index.
		private NotificationHandler[]? _notificationHandlers;
		private BatteryState? _batteryState;
		private FeatureAccessProtocol.DeviceType _deviceType;

		// NB: We probably don't need Volatile reads here, as this data isn't supposed to be updated often, and we expect it to be read as a response to a connection notification.
		public new FeatureAccessProtocol.DeviceType DeviceType
		{
			get => _deviceType;
			private protected set => Volatile.Write(ref Unsafe.As<FeatureAccessProtocol.DeviceType, byte>(ref _deviceType), (byte)value);
		}

		public event Action<FeatureAccess, BatteryPowerState>? BatteryChargeStateChanged;

		private protected FeatureAccess
		(
			object parentOrTransport,
			ushort productId,
			byte deviceIndex,
			DeviceConnectionInfo deviceConnectionInfo,
			FeatureAccessProtocol.DeviceType deviceType,
			ReadOnlyDictionary<HidPlusPlusFeature, byte>? cachedFeatures,
			string? friendlyName,
			string? serialNumber
		)
			: base(parentOrTransport, productId, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
		{
			_deviceType = deviceType;
			CachedFeatures = cachedFeatures;
			if (cachedFeatures is not null)
			{
				_notificationHandlers = new NotificationHandler[cachedFeatures.Count];
				RegisterDefaultNotificationHandlers(cachedFeatures);
			}
			var device = Transport.Devices[deviceIndex];
			device.NotificationReceived += HandleNotification;
		}

		private void RegisterDefaultNotificationHandlers(ReadOnlyDictionary<HidPlusPlusFeature, byte> features)
		{
			byte index;
			BatteryState batteryState;

			// Register one battery notification handler or the other, but not both.
			// We'll favor the newer feature in case both are available.
			if (features.TryGetValue(HidPlusPlusFeature.UnifiedBattery, out index))
			{
				batteryState = new UnifiedBatteryState(this, index);
				Volatile.Write(ref _batteryState, batteryState);
				Volatile.Write(ref _notificationHandlers![index], batteryState);
			}
			else if (features.TryGetValue(HidPlusPlusFeature.BatteryUnifiedLevelStatus, out index))
			{
				batteryState = new LegacyBatteryState(this, index);
				Volatile.Write(ref _batteryState, batteryState);
				Volatile.Write(ref _notificationHandlers![index], batteryState);
			}
		}

		public override HidPlusPlusProtocolFlavor ProtocolFlavor => HidPlusPlusProtocolFlavor.FeatureAccess;

		public bool HasBatteryInformation
			=> CachedFeatures is not null ?
				_batteryState is not null :
				throw new InvalidOperationException("The device has not yet been connected.");

		public BatteryPowerState BatteryPowerState
			=> _batteryState is not null ?
				_batteryState.PowerState :
				throw new InvalidOperationException("The device has no battery support.");

		protected virtual void HandleNotification(ReadOnlySpan<byte> message)
		{
			if (message.Length < 7) return;

			// The notification handlers should technically never be null once the device is connected and sending actual notifications.
			if (Volatile.Read(ref _notificationHandlers) is not { } handlers) return;

			ref var header = ref Unsafe.As<byte, FeatureAccessHeader>(ref MemoryMarshal.GetReference(message));

			// We should always have a properly-sized array for device notifications, but we don't want to fail in case of a problem here.
			if (header.FeatureIndex >= handlers.Length) return;

			if (handlers[header.FeatureIndex] is { } handler)
			{
				handler.HandleNotificationInternal(header.FunctionId, message[4..]);
			}
		}

		public override ValueTask DisposeAsync()
		{
			DisposeInternal(false);
			return ValueTask.CompletedTask;
		}

		private protected void DisposeInternal(bool clearState)
		{
			var device = Transport.Devices[DeviceIndex];
			device.NotificationReceived -= HandleNotification;
			if (clearState)
			{
				Volatile.Write(ref device.CustomState, null);
			}
		}

		// Can be called multiple times. It can be called on a disconnected device.
		private protected override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
		{
			if (_batteryState is { } batteryState)
			{
				await batteryState.RefreshBatteryCapabilitiesAsync(retryCount, cancellationToken).ConfigureAwait(false);
				await batteryState.RefreshBatteryStatusAsync(retryCount, cancellationToken).ConfigureAwait(false);
			}
		}

		public ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>> GetFeaturesAsync(CancellationToken cancellationToken)
			=> GetFeaturesWithRetryAsync(HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		protected ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>> GetFeaturesWithRetryAsync(int retryCount, CancellationToken cancellationToken)
			=> CachedFeatures is not null ?
				new ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>>(CachedFeatures) :
				new ValueTask<ReadOnlyDictionary<HidPlusPlusFeature, byte>>(GetFeaturesWithRetryAsyncCore(retryCount, cancellationToken));

		private async Task<ReadOnlyDictionary<HidPlusPlusFeature, byte>> GetFeaturesWithRetryAsyncCore(int retryCount, CancellationToken cancellationToken)
		{
			var features = await Transport.GetFeaturesWithRetryAsync(DeviceIndex, retryCount, cancellationToken).ConfigureAwait(false);

			// Ensure that features are only initialized once.
			if (Interlocked.CompareExchange(ref _notificationHandlers, new NotificationHandler[features.Count], null) is null)
			{
				RegisterDefaultNotificationHandlers(features);
				Volatile.Write(ref CachedFeatures, features);
			}
			else
			{
				features = Volatile.Read(ref CachedFeatures)!;
			}

			return features;
		}

		public Task<byte> GetFeatureIndexAsync(HidPlusPlusFeature feature, CancellationToken cancellationToken)
			=> Transport.GetFeatureIndexAsync(DeviceIndex, feature, cancellationToken);

		public Task<TResponseParameters> SendWithRetryAsync<TResponseParameters>
		(
			byte featureIndex,
			byte functionId,
			int retryCount,
			CancellationToken cancellationToken
		)
			where TResponseParameters : struct, IMessageResponseParameters
			=> Transport.FeatureAccessSendWithRetryAsync<TResponseParameters>(DeviceIndex, featureIndex, functionId, retryCount, cancellationToken);

		public Task SendWithRetryAsync<TRequestParameters>
		(
			byte featureIndex,
			byte functionId,
			in TRequestParameters requestParameters,
			int retryCount,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageRequestParameters
			=> Transport.FeatureAccessSendWithRetryAsync(DeviceIndex, featureIndex, functionId, requestParameters, retryCount, cancellationToken);

		public Task<TResponseParameters> SendWithRetryAsync<TRequestParameters, TResponseParameters>
		(
			byte featureIndex,
			byte functionId,
			in TRequestParameters requestParameters,
			int retryCount,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageRequestParameters
			where TResponseParameters : struct, IMessageResponseParameters
			=> Transport.FeatureAccessSendWithRetryAsync<TRequestParameters, TResponseParameters>(DeviceIndex, featureIndex, functionId, requestParameters, retryCount, cancellationToken);

		public Task<TResponseParameters> SendAsync<TResponseParameters>
		(
			byte featureIndex,
			byte functionId,
			CancellationToken cancellationToken
		)
			where TResponseParameters : struct, IMessageResponseParameters
			=> Transport.FeatureAccessSendWithRetryAsync<TResponseParameters>(DeviceIndex, featureIndex, functionId, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task SendAsync<TRequestParameters>
		(
			byte featureIndex,
			byte functionId,
			in TRequestParameters requestParameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageRequestParameters
			=> Transport.FeatureAccessSendWithRetryAsync(DeviceIndex, featureIndex, functionId, requestParameters, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		public Task<TResponseParameters> SendAsync<TRequestParameters, TResponseParameters>
		(
			byte featureIndex,
			byte functionId,
			in TRequestParameters requestParameters,
			CancellationToken cancellationToken
		)
			where TRequestParameters : struct, IMessageRequestParameters
			where TResponseParameters : struct, IMessageResponseParameters
			=> Transport.FeatureAccessSendWithRetryAsync<TRequestParameters, TResponseParameters>(DeviceIndex, featureIndex, functionId, requestParameters, HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);
	}
}
