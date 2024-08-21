using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;
using Microsoft.Extensions.Logging;

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
			protected FeatureAccess Device { get; }
			protected byte FeatureIndex { get; }
			public abstract HidPlusPlusFeature Feature { get; }

			protected FeatureHandler(FeatureAccess device, byte featureIndex)
			{
				Device = device;
				FeatureIndex = featureIndex;
			}

			internal void HandleNotificationInternal(byte eventId, ReadOnlySpan<byte> response)
			{
				try
				{
					HandleNotification(eventId, response);
				}
				catch (Exception ex)
				{
					Device.Logger.FeatureAccessFeatureHandlerException(Feature, eventId, ex);
				}
			}

			public virtual Task InitializeAsync(int retryCount, CancellationToken cancellationToken) => Task.CompletedTask;

			protected virtual void HandleNotification(byte eventId, ReadOnlySpan<byte> response) { }
		}

		private abstract class BatteryState : FeatureHandler
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
					if (device.BatteryChargeStateChanged is { } batteryChargeStateChanged)
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
					if (device.BatteryChargeStateChanged is { } batteryChargeStateChanged)
					{
						_ = Task.Run(() => batteryChargeStateChanged.Invoke(device, GetBatteryPowerState(newBatteryLevelAndStatus)));
					}
				}
			}
		}

		private sealed class DpiState : FeatureHandler
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

		private sealed class OnboardProfileState : FeatureHandler
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

		private sealed class BacklightV2State : FeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.BacklightV2;

			private ushort _backlightLevelAndCount;

			public BacklightState BacklightState => GetBacklightState(Volatile.Read(ref _backlightLevelAndCount));

			public BacklightV2State(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
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

		// Fields are not readonly because devices seen through a receiver can be discovered while disconnected.
		// The values should be updated when the device is connected.
		private protected HidPlusPlusFeatureCollection? CachedFeatures;
		// An array of notification handlers, with one slot for each feature index.
		private FeatureHandler[]? _featureHandlers;
		private BatteryState? _batteryState;
		private DpiState? _dpiState;
		private BacklightV2State? _backlightState;
		private LockKeyFeatureHandler? _lockKeyFeatureHandler;
		private OnboardProfileState? _onboardProfileState;
		private FeatureAccessProtocol.DeviceType _deviceType;

		// NB: We probably don't need Volatile reads here, as this data isn't supposed to be updated often, and we expect it to be read as a response to a connection notification.
		public new FeatureAccessProtocol.DeviceType DeviceType
		{
			get => _deviceType;
			private protected set => Volatile.Write(ref Unsafe.As<FeatureAccessProtocol.DeviceType, byte>(ref _deviceType), (byte)value);
		}

		public event Action<FeatureAccess, BatteryPowerState>? BatteryChargeStateChanged;
		public event Action<FeatureAccess, BacklightState>? BacklightStateChanged;
		public event Action<FeatureAccess, LockKeys>? LockKeysChanged;

		private protected FeatureAccess
		(
			object parentOrTransport,
			ILogger<FeatureAccess> logger,
			HidPlusPlusDeviceId[] deviceIds,
			byte mainDeviceIdIndex,
			byte deviceIndex,
			DeviceConnectionInfo deviceConnectionInfo,
			FeatureAccessProtocol.DeviceType deviceType,
			HidPlusPlusFeatureCollection? cachedFeatures,
			string? friendlyName,
			string? serialNumber
		)
			: base(parentOrTransport, logger, deviceIds, mainDeviceIdIndex, deviceIndex, deviceConnectionInfo, friendlyName, serialNumber)
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

		private void RegisterDefaultFeatureHandlers(HidPlusPlusFeatureCollection features)
		{
			byte index;
			BatteryState batteryState;

			// Register one battery notification handler or the other, but not both.
			// We'll favor the newer feature in case both are available.
			if (features.TryGetIndex(HidPlusPlusFeature.UnifiedBattery, out index))
			{
				batteryState = new UnifiedBatteryState(this, index);
				Volatile.Write(ref _batteryState, batteryState);
				Volatile.Write(ref _featureHandlers![index], batteryState);
			}
			else if (features.TryGetIndex(HidPlusPlusFeature.BatteryUnifiedLevelStatus, out index))
			{
				batteryState = new LegacyBatteryState(this, index);
				Volatile.Write(ref _batteryState, batteryState);
				Volatile.Write(ref _featureHandlers![index], batteryState);
			}

			if (features.TryGetIndex(HidPlusPlusFeature.AdjustableDpi, out index))
			{
				var dpiState = new DpiState(this, index);
				Volatile.Write(ref _dpiState, dpiState);
				Volatile.Write(ref _featureHandlers![index], dpiState);
			}

			if (features.TryGetIndex(HidPlusPlusFeature.BacklightV2, out index))
			{
				var backlightState = new BacklightV2State(this, index);
				Volatile.Write(ref _backlightState, backlightState);
				Volatile.Write(ref _featureHandlers![index], backlightState);
			}

			if (features.TryGetIndex(HidPlusPlusFeature.LockKeyState, out index))
			{
				var lockKeyFeatureHandler = new LockKeyFeatureHandler(this, index);
				Volatile.Write(ref _lockKeyFeatureHandler, lockKeyFeatureHandler);
				Volatile.Write(ref _featureHandlers![index], lockKeyFeatureHandler);
			}

			if (features.TryGetIndex(HidPlusPlusFeature.OnboardProfiles, out index))
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

		public bool HasBacklight
			=> CachedFeatures is not null ?
				_backlightState is not null :
				throw new InvalidOperationException("The device has not yet been connected.");

		public BacklightState BacklightState
			=> _backlightState is not null ?
				_backlightState.BacklightState :
				throw new InvalidOperationException("The device has no backlight support.");

		public bool HasLockKeys
			=> CachedFeatures is not null ?
				_lockKeyFeatureHandler is not null :
				throw new InvalidOperationException("The device has not yet been connected.");

		public LockKeys LockKeys
			=> _lockKeyFeatureHandler is not null ?
				_lockKeyFeatureHandler.LockKeys :
				throw new InvalidOperationException("The device has no backlight support.");

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
			Logger.FeatureAccessDeviceConnected(MainDeviceId.ProductId, FriendlyName, SerialNumber!);

			if (_featureHandlers is { } handlers)
			{
				if (CachedFeatures is { } cachedFeatures)
				{
					foreach (var feature in cachedFeatures)
					{
						if (Enum.IsDefined(feature.Feature)) Logger.FeatureAccessDeviceKnownFeature(SerialNumber!, feature.Index, feature.Feature, feature.Type, feature.Version);
						else Logger.FeatureAccessDeviceUnknownFeature(SerialNumber!, feature.Index, feature.Feature, feature.Type, feature.Version);
					}
				}

				for (int i = 0; i < handlers.Length; i++)
				{
					if (_featureHandlers[i] is { } handler)
					{
						await handler.InitializeAsync(retryCount, cancellationToken).ConfigureAwait(false);
					}
				}
			}
				
		}

		public ValueTask<HidPlusPlusFeatureCollection> GetFeaturesAsync(CancellationToken cancellationToken)
			=> GetFeaturesWithRetryAsync(HidPlusPlusTransportExtensions.DefaultRetryCount, cancellationToken);

		protected ValueTask<HidPlusPlusFeatureCollection> GetFeaturesWithRetryAsync(int retryCount, CancellationToken cancellationToken)
			=> CachedFeatures is not null ?
				new ValueTask<HidPlusPlusFeatureCollection>(CachedFeatures) :
				new ValueTask<HidPlusPlusFeatureCollection>(GetFeaturesWithRetryAsyncCore(retryCount, cancellationToken));

		private async Task<HidPlusPlusFeatureCollection> GetFeaturesWithRetryAsyncCore(int retryCount, CancellationToken cancellationToken)
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
