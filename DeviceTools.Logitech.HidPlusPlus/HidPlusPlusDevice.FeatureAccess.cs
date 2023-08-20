using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;
using Microsoft.VisualBasic;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract class FeatureAccess : HidPlusPlusDevice
	{
		// NB: This would probably be exposed somehow so that notifications can be registered externally, but the API needs to be reworked.
		// Notably, there is the problem of matching the notification handler with the device and getting the proper feature index.
		// Internally, this is handled by providing the information in the constructor, but externally, that would be a weird thing to do.
		private abstract class FeatureHandler
		{
			public abstract HidPlusPlusFeature Feature { get; }

			internal void HandleNotificationInternal(byte eventId, ReadOnlySpan<byte> response)
			{
				try
				{
					HandleNotification(eventId, response);
				}
				catch (Exception)
				{
					// TODO: Log ?
				}
			}

			public virtual async Task InitializeAsync(int retryCount, CancellationToken cancellationToken) { }

			protected virtual void HandleNotification(byte eventId, ReadOnlySpan<byte> response) { }
		}

		private abstract class InternalFeatureHandler : FeatureHandler
		{
			protected FeatureAccess Device { get; }
			protected byte FeatureIndex { get; }

			protected InternalFeatureHandler(FeatureAccess device, byte featureIndex)
			{
				Device = device;
				FeatureIndex = featureIndex;
			}

		}

		private abstract class BatteryState : InternalFeatureHandler
		{
			public abstract BatteryPowerState PowerState { get; }

			protected BatteryState(FeatureAccess device, byte featureIndex) : base(device, featureIndex) { }

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				await RefreshBatteryCapabilitiesAsync(retryCount, cancellationToken).ConfigureAwait(false);
				await RefreshBatteryStatusAsync(retryCount, cancellationToken).ConfigureAwait(false);
			}

			protected abstract Task RefreshBatteryCapabilitiesAsync(int retryCount, CancellationToken cancellationToken);
			protected abstract Task RefreshBatteryStatusAsync(int retryCount, CancellationToken cancellationToken);
		}

		private sealed class UnifiedBatteryState : BatteryState
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.UnifiedBattery;

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

			public UnifiedBatteryState(FeatureAccess device, byte featureIndex) : base(device, featureIndex) { }

			protected override async Task RefreshBatteryCapabilitiesAsync(int retryCount, CancellationToken cancellationToken)
			{
				var response = await Device.SendWithRetryAsync<UnifiedBattery.GetCapabilities.Response>
				(
					FeatureIndex,
					UnifiedBattery.GetCapabilities.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				_supportedBatteryLevels = response.SupportedBatteryLevels;
				_batteryFlags = response.BatteryFlags;
			}

			protected override async Task RefreshBatteryStatusAsync(int retryCount, CancellationToken cancellationToken)
			{
				var response = await Device.SendWithRetryAsync<UnifiedBattery.GetStatus.Response>
				(
					FeatureIndex,
					UnifiedBattery.GetStatus.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				ProcessStatusResponse(ref response);
			}

			protected override void HandleNotification(byte eventId, ReadOnlySpan<byte> response)
			{
				if (response.Length < 16) return;

				if (eventId != UnifiedBattery.GetStatus.EventId) return;

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

			public LegacyBatteryState(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
				_batteryLevelAndStatus = 0xFFFF;
			}

			protected override async Task RefreshBatteryCapabilitiesAsync(int retryCount, CancellationToken cancellationToken)
			{
				var response = await Device.SendWithRetryAsync<BatteryUnifiedLevelStatus.GetBatteryCapability.Response>
				(
					FeatureIndex,
					BatteryUnifiedLevelStatus.GetBatteryCapability.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				_levelCount = response.NumberOfLevels;
				_capabilityFlags = response.Flags;
			}

			protected override async Task RefreshBatteryStatusAsync(int retryCount, CancellationToken cancellationToken)
			{
				var response = await Device.SendWithRetryAsync<BatteryUnifiedLevelStatus.GetBatteryLevelStatus.Response>
				(
					FeatureIndex,
					BatteryUnifiedLevelStatus.GetBatteryLevelStatus.FunctionId,
					retryCount,
					cancellationToken
				).ConfigureAwait(false);

				ProcessStatusResponse(ref response);
			}

			protected override void HandleNotification(byte eventId, ReadOnlySpan<byte> response)
			{
				if (response.Length < 16) return;

				if (eventId != BatteryUnifiedLevelStatus.GetBatteryLevelStatus.EventId) return;

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

		private sealed class DpiState : InternalFeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.AdjustableDpi;

			private byte _sensorCount;

			public DpiState(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
			}

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				byte sensorCount =
				(
					await Device.SendWithRetryAsync<AdjustableDpi.GetSensorCount.Response>(FeatureIndex, AdjustableDpi.GetSensorCount.FunctionId, retryCount, cancellationToken)
						.ConfigureAwait(false)
				).SensorCount;

				// Can't make any sense of these informations. Usefulness of this feature may depend on the model, but everything reported here seems relatively inaccurate.
				// The DPI list seemed to include the max DPI supported by the mouse, which is good, but the current DPI info seems to only be valid in host mode.
				// Other infos were pure rubbish ?
				//for (int i = 0; i < sensorCount; i++)
				//{
				//	var dpiInformation = await Device.SendWithRetryAsync<AdjustableDpi.GetSensorDpi.Request, AdjustableDpi.GetSensorDpi.Response>
				//	(
				//		FeatureIndex,
				//		AdjustableDpi.GetSensorDpi.FunctionId,
				//		new() { SensorIndex = (byte)i },
				//		retryCount,
				//		cancellationToken
				//	).ConfigureAwait(false);

				//	var dpiList = await Device.SendWithRetryAsync<AdjustableDpi.GetSensorDpiList.Request, AdjustableDpi.GetSensorDpiList.Response>
				//	(
				//		FeatureIndex,
				//		AdjustableDpi.GetSensorDpiList.FunctionId,
				//		new() { SensorIndex = (byte)i },
				//		retryCount,
				//		cancellationToken
				//	).ConfigureAwait(false);
				//}

				_sensorCount = sensorCount;
			}
		}

		private sealed class OnboardProfileState : InternalFeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.OnboardProfiles;

			public OnboardProfileState(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
			}

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				//var data = await Device.SendWithRetryAsync<RawLongMessageParameters>(FeatureIndex, 0, retryCount, cancellationToken).ConfigureAwait(false);
			}
		}

		// Fields are not readonly because devices seen through a receiver can be discovered while disconnected.
		// The values should be updated when the device is connected.
		private protected ReadOnlyDictionary<HidPlusPlusFeature, byte>? CachedFeatures;
		// An array of notification handlers, with one slot for each feature index.
		private FeatureHandler[]? _featureHandlers;
		private BatteryState? _batteryState;
		private DpiState? _dpiState;
		private OnboardProfileState? _onboardProfileState;
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
				_featureHandlers = new FeatureHandler[cachedFeatures.Count];
				RegisterDefaultFeatureHandlers(cachedFeatures);
			}
			var device = Transport.Devices[deviceIndex];
			device.NotificationReceived += HandleNotification;
		}

		private void RegisterDefaultFeatureHandlers(ReadOnlyDictionary<HidPlusPlusFeature, byte> features)
		{
			byte index;
			BatteryState batteryState;

			// Register one battery notification handler or the other, but not both.
			// We'll favor the newer feature in case both are available.
			if (features.TryGetValue(HidPlusPlusFeature.UnifiedBattery, out index))
			{
				batteryState = new UnifiedBatteryState(this, index);
				Volatile.Write(ref _batteryState, batteryState);
				Volatile.Write(ref _featureHandlers![index], batteryState);
			}
			else if (features.TryGetValue(HidPlusPlusFeature.BatteryUnifiedLevelStatus, out index))
			{
				batteryState = new LegacyBatteryState(this, index);
				Volatile.Write(ref _batteryState, batteryState);
				Volatile.Write(ref _featureHandlers![index], batteryState);
			}

			if (features.TryGetValue(HidPlusPlusFeature.AdjustableDpi, out index))
			{
				var dpiState = new DpiState(this, index);
				Volatile.Write(ref _dpiState, dpiState);
				Volatile.Write(ref _featureHandlers![index], dpiState);
			}

			if (features.TryGetValue(HidPlusPlusFeature.OnboardProfiles, out index))
			{
				var onboardProfileState = new OnboardProfileState(this, index);
				Volatile.Write(ref _onboardProfileState, onboardProfileState);
				Volatile.Write(ref _featureHandlers![index], onboardProfileState);
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
			if (Volatile.Read(ref _featureHandlers) is not { } handlers) return;

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
			if (_featureHandlers is { } handlers)
			{
				for (int i = 0; i < handlers.Length; i++)
				{
					if (_featureHandlers[i] is { } handler)
					{
						await handler.InitializeAsync(retryCount, cancellationToken).ConfigureAwait(false);
					}
				}
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
			if (Interlocked.CompareExchange(ref _featureHandlers, new FeatureHandler[features.Count], null) is null)
			{
				RegisterDefaultFeatureHandlers(features);
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
