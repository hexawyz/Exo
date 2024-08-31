using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;
using Microsoft.Extensions.Logging;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess : HidPlusPlusDevice
	{
		// Fields are not readonly because devices seen through a receiver can be discovered while disconnected.
		// The values should be updated when the device is connected.
		private protected HidPlusPlusFeatureCollection? CachedFeatures;
		// An array of notification handlers, with one slot for each feature index.
		private FeatureHandler[]? _featureHandlers;
		private BatteryFeatureHandler? _batteryState;
		private DpiFeatureHandler? _dpiState;
		private ReportRateFeatureHandler? _reportRateState;
		private BacklightV2FeatureHandler? _backlightState;
		private ColorLedEffectFeatureHandler? _colorLedEffectState;
		private LockKeyFeatureHandler? _lockKeyFeatureHandler;
		private OnboardProfileFeatureHandler? _onBoardProfileState;
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
		public event Action<FeatureAccess, DpiStatus>? DpiChanged;
		public event Action<FeatureAccess, byte?>? ProfileChanged;

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
			HidPlusPlusFeatureInformation info;
			BatteryFeatureHandler batteryState;

			// Register one battery notification handler or the other, but not both.
			// We'll favor the newer feature in case both are available.
			if (features.TryGetIndex(HidPlusPlusFeature.UnifiedBattery, out index))
			{
				batteryState = new UnifiedBatteryFeatureHandler(this, index);
				Volatile.Write(ref _batteryState, batteryState);
				Volatile.Write(ref _featureHandlers![index], batteryState);
			}
			else if (features.TryGetIndex(HidPlusPlusFeature.BatteryUnifiedLevelStatus, out index))
			{
				batteryState = new LegacyBatteryFeatureHandler(this, index);
				Volatile.Write(ref _batteryState, batteryState);
				Volatile.Write(ref _featureHandlers![index], batteryState);
			}

			if (features.TryGetIndex(HidPlusPlusFeature.AdjustableDpi, out index))
			{
				var dpiState = new DpiFeatureHandler(this, index);
				Volatile.Write(ref _dpiState, dpiState);
				Volatile.Write(ref _featureHandlers![index], dpiState);
			}

			if (features.TryGetIndex(HidPlusPlusFeature.ReportRate, out index))
			{
				var reportRate = new ReportRateFeatureHandler(this, index);
				Volatile.Write(ref _reportRateState, reportRate);
				Volatile.Write(ref _featureHandlers![index], reportRate);
			}

			if (features.TryGetIndex(HidPlusPlusFeature.BacklightV2, out index))
			{
				var backlightState = new BacklightV2FeatureHandler(this, index);
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
				var onboardProfileState = new OnboardProfileFeatureHandler(this, index);
				Volatile.Write(ref _onBoardProfileState, onboardProfileState);
				Volatile.Write(ref _featureHandlers![index], onboardProfileState);
			}

			// Only setup the "Color LED Effects" feature if it is indicated as public. Otherwise, it could be something else, but it is locked behind some unknown access mechanism.
			if (features.TryGetInformation(HidPlusPlusFeature.ColorLedEffects, out info) && (info.Type & (HidPlusPlusFeatureTypes.Engineering | HidPlusPlusFeatureTypes.Hidden)) == 0)
			{
				var colorLedEffectState = new ColorLedEffectFeatureHandler(this, info.Index);
				Volatile.Write(ref _colorLedEffectState, colorLedEffectState);
				Volatile.Write(ref _featureHandlers![info.Index], colorLedEffectState);
			}
		}

		public override HidPlusPlusProtocolFlavor ProtocolFlavor => HidPlusPlusProtocolFlavor.FeatureAccess;

		public bool HasBatteryInformation => HasFeature(_batteryState);
		public BatteryPowerState BatteryPowerState => GetFeature(in _batteryState).PowerState;

		public bool HasBacklight => HasFeature(_backlightState);
		public BacklightState BacklightState => GetFeature(in _backlightState).BacklightState;

		public bool HasLockKeys => HasFeature(_lockKeyFeatureHandler);
		public LockKeys LockKeys => GetFeature(in _lockKeyFeatureHandler).LockKeys;

		public bool HasAdjustableDpi => HasFeature(_dpiState);
		public DpiStatus CurrentDpi
		{
			get
			{
				var dpiFeature = GetFeature(in _dpiState);
				if (HasOnBoardProfiles)
				{
					var onBoardProfileFeature = GetFeature(in _onBoardProfileState);
					if (onBoardProfileFeature.DeviceMode == DeviceMode.OnBoardMemory)
					{
						byte presetIndex = onBoardProfileFeature.CurrentDpiIndex;
						return new(presetIndex, new(onBoardProfileFeature.CurrentProfile.DpiPresets[presetIndex]));
					}
				}
				return new(new(dpiFeature.CurrentDpi));
			}
		}

		public ImmutableArray<DpiRange> DpiRanges => GetFeature(in _dpiState).DpiRanges;

		public bool HasAdjustableReportInterval => HasFeature(_reportRateState);
		public ReportIntervals SupportedReportIntervals => GetFeature(in _reportRateState).SupportedReportIntervals;
		public byte ReportInterval => GetFeature(in _reportRateState).ReportInterval;

		public async Task SetReportIntervalAsync(byte reportInterval, CancellationToken cancellationToken)
		{
			var feature = GetFeature(in _reportRateState);
			if (reportInterval > 8 || ((byte)feature.SupportedReportIntervals & (1 << (reportInterval - 1))) == 0) throw new ArgumentOutOfRangeException(nameof(reportInterval));
			await feature.SetReportIntervalAsync(reportInterval, cancellationToken).ConfigureAwait(false);
		}

		internal void OnDpiChanged(DpiStatus dpi)
			=> DpiChanged?.Invoke(this, dpi);

		internal void OnProfileChanged(byte? profileIndex)
			=> ProfileChanged?.Invoke(this, profileIndex);

		public bool HasOnBoardProfiles => HasFeature(_onBoardProfileState);

		public byte ProfileCount => GetFeature(in _onBoardProfileState).ProfileCount;
		public byte? CurrentProfileIndex => GetFeature(in _onBoardProfileState).CurrentProfileIndex;

		public ImmutableArray<DotsPerInch> GetCurrentDpiPresets()
		{
			var onBoardProfileFeature = GetFeature(in _onBoardProfileState);

			if (onBoardProfileFeature.DeviceMode == DeviceMode.Host) return [];

			var rawPresets = onBoardProfileFeature.CurrentProfile.DpiPresets;

			int i = 0;
			while (i < rawPresets.Count)
			{
				if (rawPresets[i] == 0) break;
				i++;
			}

			var presets = new DotsPerInch[i];
			for (i = 0; i < presets.Length; i++)
			{
				presets[i] = new(rawPresets[i]);
			}

			return ImmutableCollectionsMarshal.AsImmutableArray(presets);
		}

		public async Task SetCurrentDpiPresetAsync(byte dpiPresetIndex, CancellationToken cancellationToken)
		{
			var feature = GetFeature(in _onBoardProfileState);
			await feature.SetActiveDpiIndex(dpiPresetIndex, cancellationToken).ConfigureAwait(false);
		}

		private bool HasFeature(object? featureHandler)
			=> CachedFeatures is not null ?
				featureHandler is not null :
				throw new InvalidOperationException("The device has not yet been connected.");

		private static T GetFeature<T>(in T? featureHandler) where T : FeatureHandler
			=> featureHandler ?? throw new InvalidOperationException("The device does not support this feature.");

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

		public override async ValueTask DisposeAsync(bool parentDisposed)
		{
			// NB: This is called internally in synchronous code, so it is important that the synchronous part comes first.
			// The asynchronous part only concerns the feature handlers, which may need to shutdown when the device object is not used anymore.
			var device = Transport.Devices[DeviceIndex];
			device.NotificationReceived -= HandleNotification;
			if (parentDisposed)
			{
				Volatile.Write(ref device.CustomState, null);
				if (_featureHandlers is { } handlers)
				{
					foreach (var handler in handlers)
					{
						try
						{
							await handler.DisposeAsync().ConfigureAwait(false);
						}
						catch
						{
						}
					}
				}
			}
		}

		// Can be called multiple times. It can be called on a disconnected device.
		private protected override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
		{
			Logger.FeatureAccessDeviceConnected(MainDeviceId.ProductId, FriendlyName, SerialNumber!);

			if (_featureHandlers is { } handlers)
			{
				foreach (var feature in CachedFeatures!)
				{
					if (Enum.IsDefined(feature.Feature)) Logger.FeatureAccessDeviceKnownFeature(SerialNumber!, feature.Index, feature.Feature, feature.Type, feature.Version);
					else Logger.FeatureAccessDeviceUnknownFeature(SerialNumber!, feature.Index, feature.Feature, feature.Type, feature.Version);
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

		private protected override void Reset()
		{
			if (_featureHandlers is { } handlers)
			{
				for (int i = 0; i < handlers.Length; i++)
				{
					if (_featureHandlers[i] is { } handler)
					{
						handler.Reset();
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

		public Task SendWithRetryAsync
		(
			byte featureIndex,
			byte functionId,
			int retryCount,
			CancellationToken cancellationToken
		)
			=> Transport.FeatureAccessSendWithRetryAsync(DeviceIndex, featureIndex, functionId, retryCount, cancellationToken);

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
