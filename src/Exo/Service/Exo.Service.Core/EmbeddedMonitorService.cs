using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Features;
using Exo.Features.EmbeddedMonitors;
using Exo.Images;
using Exo.Monitors;
using Exo.Programming.Annotations;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

[Module("EmbeddedMonitor")]
[TypeId(0x94206A36, 0x5C50, 0x4F65, 0xBA, 0x94, 0xE8, 0x01, 0xC5, 0x0E, 0x45, 0xA9)]
internal sealed partial class EmbeddedMonitorService : IAsyncDisposable
{
	private sealed class DeviceState
	{
		private Driver? _driver;
		public Driver? Driver => _driver;
		public Dictionary<Guid, EmbeddedMonitorState> EmbeddedMonitors { get; }
		private readonly AsyncLock _lock;
		public AsyncLock Lock => _lock;
		public IConfigurationContainer DeviceConfigurationContainer { get; }
		public IConfigurationContainer<Guid> EmbeddedMonitorConfigurationContainer { get; }
		private readonly Guid _id;
		public Guid Id => _id;

		public DeviceState
		(
			Guid id,
			IConfigurationContainer deviceConfigurationContainer,
			IConfigurationContainer<Guid> embeddedMonitorsConfigurationContainer,
			Dictionary<Guid, EmbeddedMonitorState> embeddedMonitors
		)
		{
			_id = id;
			DeviceConfigurationContainer = deviceConfigurationContainer;
			EmbeddedMonitorConfigurationContainer = embeddedMonitorsConfigurationContainer;
			EmbeddedMonitors = embeddedMonitors;
			_lock = new();
		}

		public void SetOnline(Driver driver)
		{
			_driver = driver;
		}

		public void SetOffline()
		{
			Volatile.Write(ref _driver, null);
		}

		//public PersistedEmbeddedMonitorDeviceInformation CreatePersistedConfiguration() => new();

		//public LightingDeviceConfigurationWatchNotification CreateConfigurationWatchNotification(Guid deviceId) 
		//	=> new() { DeviceId = deviceId, IsUnifiedLightingEnabled = IsUnifiedLightingEnabled, BrightnessLevel = Brightness };

		public EmbeddedMonitorDeviceInformation CreateInformation()
		{
			var monitors = EmbeddedMonitors.Count > 0 ? new EmbeddedMonitorInformation[EmbeddedMonitors.Count] : [];
			int i = 0;
			foreach (var monitor in EmbeddedMonitors.Values)
			{
				monitors[i++] = monitor.CreateInformation();
			}
			return new EmbeddedMonitorDeviceInformation() { DeviceId = _id, EmbeddedMonitors = ImmutableCollectionsMarshal.AsImmutableArray(monitors) };
		}
	}

	private sealed class EmbeddedMonitorState
	{
		private static readonly ImmutableArray<EmbeddedMonitorGraphicsDescription> DefaultSupportedGraphics = [EmbeddedMonitorGraphicsDescription.CustomGraphics];
		private IEmbeddedMonitor? _monitor;

		// Current configuration
		private Guid _currentGraphics;
		private UInt128 _currentImageId;
		private ushort _currentRegionLeft;
		private ushort _currentRegionTop;
		private ushort _currentRegionWidth;
		private ushort _currentRegionHeight;

		// Metadata
		private readonly Guid _id;
		private MonitorShape _shape;
		private ushort _width;
		private ushort _height;
		private PixelFormat _pixelFormat;
		private ImageFormats _imageFormats;
		private EmbeddedMonitorCapabilities _capabilities;
		private ImmutableArray<EmbeddedMonitorGraphicsDescription> _supportedGraphics;
		private HashSet<Guid>? _supportedBuiltInGraphicIds;

		public EmbeddedMonitorState(Guid id)
		{
			_id = id;
			_supportedGraphics = [];
		}

		public EmbeddedMonitorState
		(
			Guid id,
			MonitorShape shape,
			ushort width,
			ushort height,
			PixelFormat pixelFormat,
			ImageFormats imageFormats,
			EmbeddedMonitorCapabilities capabilities,
			ImmutableArray<EmbeddedMonitorGraphicsDescription> supportedGraphics,
			PersistedMonitorConfiguration configuration
		)
		{
			_id = id;
			_shape = shape;
			_width = width;
			_height = height;
			_pixelFormat = pixelFormat;
			_imageFormats = imageFormats;
			_capabilities = capabilities;
			_supportedGraphics = supportedGraphics;
			if ((_capabilities & EmbeddedMonitorCapabilities.BuiltInGraphics) != 0 && supportedGraphics.Length > 0 && (supportedGraphics.Length > 1 || supportedGraphics[0].GraphicsId != default))
			{
				_supportedBuiltInGraphicIds = new();
				foreach (var g in supportedGraphics)
				{
					if (g.GraphicsId == default) continue;
					_supportedBuiltInGraphicIds.Add(g.GraphicsId);
				}
				// This is mostly a safeguard for bogus state. It should not be needed but it is better to guard against bugs in drivers.
				if (_supportedBuiltInGraphicIds.Count == 0) _supportedBuiltInGraphicIds = null;
			}
			_currentGraphics = configuration.GraphicsId;
			_currentImageId = configuration.ImageId;
			_currentRegionLeft = (ushort)configuration.ImageRegion.Left;
			_currentRegionTop = (ushort)configuration.ImageRegion.Top;
			_currentRegionWidth = (ushort)configuration.ImageRegion.Width;
			_currentRegionHeight = (ushort)configuration.ImageRegion.Height;
		}

		public bool SetOnline(IEmbeddedMonitor monitor)
		{
			var information = monitor.MonitorInformation;

			EmbeddedMonitorCapabilities capabilities = EmbeddedMonitorCapabilities.None;
			ImmutableArray<EmbeddedMonitorGraphicsDescription> supportedGraphics;

			if (information.HasAnimationSupport) capabilities |= EmbeddedMonitorCapabilities.AnimatedImages;

			if (monitor is IEmbeddedMonitorBuiltInGraphics builtInGraphics)
			{
				bool hasBuiltInGraphics = false;
				supportedGraphics = builtInGraphics.SupportedGraphics;
				foreach (var item in supportedGraphics)
				{
					if (item.GraphicsId == default)
					{
						capabilities |= EmbeddedMonitorCapabilities.StaticImages;
					}
					else
					{
						hasBuiltInGraphics = true;
					}
				}
				if (hasBuiltInGraphics) capabilities |= EmbeddedMonitorCapabilities.BuiltInGraphics;
			}
			else
			{
				capabilities |= EmbeddedMonitorCapabilities.StaticImages;
				supportedGraphics = DefaultSupportedGraphics;
			}

			if (monitor is IDrawableEmbeddedMonitor drawable)
			{
				capabilities |= EmbeddedMonitorCapabilities.PartialUpdates;
			}

			bool hasChanged = false;

			if (_shape != information.Shape)
			{
				_shape = information.Shape;
				hasChanged = true;
			}
			if (_width != information.ImageSize.Width)
			{
				_width = checked((ushort)information.ImageSize.Width);
				hasChanged = true;
			}
			if (_height != information.ImageSize.Height)
			{
				_height = checked((ushort)information.ImageSize.Height);
				hasChanged = true;
			}
			if (_imageFormats != information.SupportedImageFormats)
			{
				_imageFormats = information.SupportedImageFormats;
				hasChanged = true;
			}
			if (_pixelFormat != information.PixelFormat)
			{
				_pixelFormat = information.PixelFormat;
				hasChanged = true;
			}
			if (_capabilities != capabilities)
			{
				_capabilities = capabilities;
				hasChanged = true;
			}
			if (!supportedGraphics.SequenceEqual(_supportedGraphics))
			{
				_supportedGraphics = supportedGraphics;
				_supportedBuiltInGraphicIds?.Clear();
				if ((_capabilities & EmbeddedMonitorCapabilities.BuiltInGraphics) != 0 && supportedGraphics.Length > 0 && (supportedGraphics.Length > 1 || supportedGraphics[0].GraphicsId != default))
				{
					_supportedBuiltInGraphicIds ??= new();
					foreach (var g in supportedGraphics)
					{
						if (g.GraphicsId == default) continue;
						_supportedBuiltInGraphicIds.Add(g.GraphicsId);
					}
				}
				// This is mostly a safeguard for bogus state. It should not be needed but it is better to guard against bugs in drivers.
				if (_supportedBuiltInGraphicIds?.Count == 0) _supportedBuiltInGraphicIds = null;
				hasChanged = true;
			}

			_monitor = monitor;

			return hasChanged;
		}

		public void SetOffline()
		{
			_monitor = null;
		}

		public async ValueTask<bool> SetBuiltInGraphicsAsync(Guid graphicsId, CancellationToken cancellationToken)
		{
			if (_supportedBuiltInGraphicIds?.Contains(graphicsId) != true) throw new ArgumentOutOfRangeException(nameof(graphicsId));

			if (graphicsId != _currentGraphics)
			{
				if (_monitor is IEmbeddedMonitorBuiltInGraphics builtInGraphics)
				{
					await builtInGraphics.SetCurrentModeAsync(graphicsId, cancellationToken).ConfigureAwait(false);
				}

				_currentGraphics = graphicsId;
				_currentImageId = 0;
				_currentRegionLeft = 0;
				_currentRegionTop = 0;
				_currentRegionWidth = 0;
				_currentRegionHeight = 0;

				return true;
			}
			return false;
		}

		public async ValueTask<bool> SetImageAsync(ImageStorageService imageStorageService, UInt128 imageId, Rectangle region, CancellationToken cancellationToken)
		{
			if (imageId != _currentImageId || region.Left != _currentRegionLeft || region.Top != _currentRegionTop || region.Width != _currentRegionWidth || region.Height != _currentRegionHeight)
			{
				if (_monitor is not null)
				{
					var (physicalImageId, imageFormat, imageFile) = imageStorageService.GetTransformedImage
					(
						imageId,
						new(region.Left, region.Top, region.Width, region.Height),
						_imageFormats,
						(_capabilities & EmbeddedMonitorCapabilities.AnimatedImages) != 0 ? _imageFormats & ImageFormats.Gif : 0,
						new(_width, _height),
						_shape == MonitorShape.Circle
					);
					using (imageFile)
					using (var memoryManager = imageFile.CreateMemoryManager())
					{
						await _monitor.SetImageAsync(physicalImageId, imageFormat, memoryManager.Memory, cancellationToken);
					}
				}

				_currentGraphics = default;
				_currentImageId = imageId;
				_currentRegionLeft = (ushort)region.Left;
				_currentRegionTop = (ushort)region.Top;
				_currentRegionWidth = (ushort)region.Width;
				_currentRegionHeight = (ushort)region.Height;

				return true;
			}
			return false;
		}

		public async ValueTask RestoreConfigurationAsync(ImageStorageService imageStorageService, CancellationToken cancellationToken)
		{
			if (_currentGraphics != default)
			{
				if (_monitor is IEmbeddedMonitorBuiltInGraphics builtInGraphics)
				{
					await builtInGraphics.SetCurrentModeAsync(_currentGraphics, cancellationToken).ConfigureAwait(false);
				}
			}
			else if (_currentImageId != 0)
			{
				// TODO
			}
		}

		public PersistedEmbeddedMonitorInformation CreatePersistedInformation()
			=> new()
			{
				Shape = _shape,
				Width = _width,
				Height = _height,
				PixelFormat = _pixelFormat,
				ImageFormats = _imageFormats,
				Capabilities = _capabilities,
				SupportedGraphics = _supportedGraphics,
			};

		public EmbeddedMonitorInformation CreateInformation()
			=> new()
			{
				MonitorId = _id,
				Shape = _shape,
				ImageSize = new(_width, _height),
				PixelFormat = _pixelFormat,
				SupportedImageFormats = _imageFormats,
				Capabilities = _capabilities,
				SupportedGraphics = _supportedGraphics,
			};

		public PersistedMonitorConfiguration CreatePersistedConfiguration()
			=> new()
			{
				GraphicsId = _currentGraphics,
				ImageId = _currentImageId,
				ImageRegion = new(_currentRegionLeft, _currentRegionTop, _currentRegionWidth, _currentRegionHeight)
			};

		public EmbeddedMonitorConfigurationWatchNotification CreateConfigurationNotification(Guid deviceId)
			=> new()
			{
				DeviceId = deviceId,
				MonitorId = _id,
				GraphicsId = _currentGraphics,
				ImageId = _currentImageId,
				ImageRegion = new(_currentRegionLeft, _currentRegionTop, _currentRegionWidth, _currentRegionHeight)
			};
	}

	//[TypeId(0xC49F1625, 0xA7D0, 0x4FC8, 0x8B, 0x43, 0x2F, 0xCF, 0x0B, 0x76, 0xF7, 0xD5)]
	//private readonly struct PersistedEmbeddedMonitorDeviceInformation
	//{
	//}

	[TypeId(0xA497F88F, 0xB13F, 0x429D, 0xA3, 0x5D, 0xA3, 0x67, 0x07, 0x7B, 0x05, 0x93)]
	private readonly struct PersistedEmbeddedMonitorInformation
	{
		public required MonitorShape Shape { get; init; }
		public required ushort Width { get; init; }
		public required ushort Height { get; init; }
		public required PixelFormat PixelFormat { get; init; }
		public required ImageFormats ImageFormats { get; init; }
		public required EmbeddedMonitorCapabilities Capabilities { get; init; }
		public ImmutableArray<EmbeddedMonitorGraphicsDescription> SupportedGraphics { get; init; }
	}

	[TypeId(0x5A84D766, 0x721A, 0x478A, 0xA8, 0xF7, 0x51, 0x99, 0xED, 0x9A, 0xE0, 0x54)]
	private readonly struct PersistedMonitorConfiguration
	{
		public Guid GraphicsId { get; init; }
		public UInt128 ImageId { get; init; }
		public Rectangle ImageRegion { get; init; }
	}

	private const string EmbeddedMonitorConfigurationContainerName = "scr";

	public static async ValueTask<EmbeddedMonitorService> CreateAsync
	(
		ILogger<EmbeddedMonitorService> logger,
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		IDeviceWatcher deviceWatcher,
		ImageStorageService imageStorageService,
		CancellationToken cancellationToken
	)
	{
		var deviceIds = await devicesConfigurationContainer.GetKeysAsync(cancellationToken).ConfigureAwait(false);

		var deviceStates = new ConcurrentDictionary<Guid, DeviceState>();

		foreach (var deviceId in deviceIds)
		{
			var deviceConfigurationContainer = devicesConfigurationContainer.GetContainer(deviceId);

			if (deviceConfigurationContainer.TryGetContainer(EmbeddedMonitorConfigurationContainerName, GuidNameSerializer.Instance) is not { } embeddedMonitorConfigurationContainer)
			{
				continue;
			}

			var embeddedMonitorIds = await embeddedMonitorConfigurationContainer.GetKeysAsync(cancellationToken);

			if (embeddedMonitorIds.Length == 0)
			{
				continue;
			}

			var embeddedMonitors = new Dictionary<Guid, EmbeddedMonitorState>();

			foreach (var embeddedMonitorId in embeddedMonitorIds)
			{
				PersistedEmbeddedMonitorInformation info;
				{
					var result = await embeddedMonitorConfigurationContainer.ReadValueAsync<PersistedEmbeddedMonitorInformation>(embeddedMonitorId, cancellationToken).ConfigureAwait(false);
					if (!result.Found) continue;
					info = result.Value;
				}
				PersistedMonitorConfiguration configuration;
				{
					var result = await embeddedMonitorConfigurationContainer.ReadValueAsync<PersistedMonitorConfiguration>(embeddedMonitorId, cancellationToken).ConfigureAwait(false);
					if (!result.Found) configuration = default;
					else configuration = result.Value;
				}
				var state = new EmbeddedMonitorState
				(
					embeddedMonitorId,
					info.Shape,
					info.Width,
					info.Height,
					info.PixelFormat,
					info.ImageFormats,
					info.Capabilities,
					info.SupportedGraphics,
					configuration
				);
				embeddedMonitors.Add(embeddedMonitorId, state);
			}

			if (embeddedMonitors.Count > 0)
			{
				deviceStates.TryAdd
				(
					deviceId,
					new DeviceState
					(
						deviceId,
						deviceConfigurationContainer,
						embeddedMonitorConfigurationContainer,
						embeddedMonitors
					)
				);
			}
		}

		return new EmbeddedMonitorService(logger, devicesConfigurationContainer, deviceWatcher, imageStorageService, deviceStates);
	}

	private readonly IDeviceWatcher _deviceWatcher;
	private readonly ConcurrentDictionary<Guid, DeviceState> _embeddedMonitorDeviceStates;
	private readonly ImageStorageService _imageStorageService;
	private readonly AsyncLock _lock;
	private ChannelWriter<EmbeddedMonitorDeviceInformation>[]? _deviceListeners;
	private ChannelWriter<EmbeddedMonitorConfigurationWatchNotification>[]? _configurationChangeListeners;
	private readonly IConfigurationContainer<Guid> _devicesConfigurationContainer;
	private readonly ILogger<EmbeddedMonitorService> _logger;

	private CancellationTokenSource? _cancellationTokenSource;
	private readonly Task _watchTask;

	private EmbeddedMonitorService
	(
		ILogger<EmbeddedMonitorService> logger,
		IConfigurationContainer<Guid> devicesConfigurationContainer,
		IDeviceWatcher deviceWatcher,
		ImageStorageService imageStorageService,
		ConcurrentDictionary<Guid, DeviceState> embeddedMonitorDeviceStates
	)
	{
		_lock = new();
		_logger = logger;
		_devicesConfigurationContainer = devicesConfigurationContainer;
		_deviceWatcher = deviceWatcher;
		_imageStorageService = imageStorageService;
		_embeddedMonitorDeviceStates = embeddedMonitorDeviceStates;
		_cancellationTokenSource = new();
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
			cts.Dispose();
			await _watchTask.ConfigureAwait(false);
		}
	}

	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _deviceWatcher.WatchAvailableAsync<IEmbeddedMonitorDeviceFeature>(cancellationToken))
			{
				switch (notification.Kind)
				{
				case WatchNotificationKind.Enumeration:
				case WatchNotificationKind.Addition:
					try
					{
						using (await _lock.WaitAsync(cancellationToken))
						{
							await HandleArrivalAsync(notification, cancellationToken).ConfigureAwait(false);
						}
					}
					catch (Exception ex)
					{
						_logger.LightingServiceDeviceArrivalError(notification.DeviceInformation.Id, notification.DeviceInformation.FriendlyName, ex);
					}
					break;
				case WatchNotificationKind.Removal:
					try
					{
						using (await _lock.WaitAsync(cancellationToken))
						{
							await OnDriverRemovedAsync(notification, cancellationToken).ConfigureAwait(false);
						}
					}
					catch (Exception ex)
					{
						_logger.LightingServiceDeviceRemovalError(notification.DeviceInformation.Id, notification.DeviceInformation.FriendlyName, ex);
					}
					break;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async ValueTask HandleArrivalAsync(DeviceWatchNotification notification, CancellationToken cancellationToken)
	{
		var embeddedMonitorFeatures = (IDeviceFeatureSet<IEmbeddedMonitorDeviceFeature>)notification.FeatureSet!;

		var embeddedMonitorControllerFeature = embeddedMonitorFeatures.GetFeature<IEmbeddedMonitorControllerFeature>();
		var embeddedMonitors = embeddedMonitorControllerFeature?.EmbeddedMonitors ?? [];

		var screenSaverFeature = embeddedMonitorFeatures.GetFeature<IEmbeddedMonitorScreenSaverFeature>();
		if (screenSaverFeature is not null)
		{
		}

		// Either the screensaver feature or the controller feature is required.
		if (embeddedMonitorFeatures is null && screenSaverFeature is null)
		{
			// TODO: Log a warning.
			return;
		}

		var changedMonitors = new HashSet<Guid>();
		bool isNew = false;

		if (!_embeddedMonitorDeviceStates.TryGetValue(notification.DeviceInformation.Id, out var deviceState))
		{
			var deviceConfigurationContainer = _devicesConfigurationContainer.GetContainer(notification.DeviceInformation.Id);
			deviceState = new DeviceState
			(
				notification.DeviceInformation.Id,
				deviceConfigurationContainer,
				deviceConfigurationContainer.GetContainer(EmbeddedMonitorConfigurationContainerName, GuidNameSerializer.Instance),
				new()
			);
			isNew = true;
		}

		using (await deviceState.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (embeddedMonitorControllerFeature is not null)
			{
				foreach (var oldMonitorId in deviceState.EmbeddedMonitors.Keys)
				{
					changedMonitors.Add(oldMonitorId);
				}
				foreach (var monitor in embeddedMonitors)
				{
					changedMonitors.Remove(monitor.MonitorId);
				}
				foreach (var deletedMonitorId in changedMonitors)
				{
					if (deviceState.EmbeddedMonitors.Remove(deletedMonitorId))
					{
						await deviceState.EmbeddedMonitorConfigurationContainer.DeleteValuesAsync(deletedMonitorId);
					}
				}
				changedMonitors.Clear();
				foreach (var monitor in embeddedMonitors)
				{
					if (!deviceState.EmbeddedMonitors.TryGetValue(monitor.MonitorId, out var monitorState))
					{
						deviceState.EmbeddedMonitors.TryAdd(monitor.MonitorId, monitorState = new(monitor.MonitorId));
					}

					if (monitorState.SetOnline(monitor))
					{
						changedMonitors.Add(monitor.MonitorId);
					}
				}
			}

			deviceState.SetOnline(notification.Driver!);

			if (isNew)
			{
				_embeddedMonitorDeviceStates.TryAdd(deviceState.Id, deviceState);
			}
			else
			{
				foreach (var monitorState in deviceState.EmbeddedMonitors.Values)
				{
					await monitorState.RestoreConfigurationAsync(_imageStorageService, cancellationToken).ConfigureAwait(false);
				}
			}

			foreach (var changedMonitorId in changedMonitors)
			{
				if (deviceState.EmbeddedMonitors.TryGetValue(changedMonitorId, out var monitorState))
				{
					await deviceState.EmbeddedMonitorConfigurationContainer.WriteValueAsync(changedMonitorId, monitorState.CreatePersistedInformation(), cancellationToken).ConfigureAwait(false);
				}
			}

			if (_deviceListeners is { } deviceListeners)
			{
				deviceListeners.TryWrite(deviceState.CreateInformation());
			}
		}
	}

	private async ValueTask OnDriverRemovedAsync(DeviceWatchNotification notification, CancellationToken cancellationToken)
	{
		if (_embeddedMonitorDeviceStates.TryGetValue(notification.DeviceInformation.Id, out var deviceState))
		{
			using (await deviceState.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				deviceState.SetOffline();
			}
		}
	}

	public async ValueTask SetBuiltInGraphicsAsync(Guid deviceId, Guid monitorId, Guid graphicsId, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfEqual(graphicsId, default);

		if (!_embeddedMonitorDeviceStates.TryGetValue(deviceId, out var deviceState)) throw new InvalidOperationException("Device not found.");

		PersistedMonitorConfiguration configuration;

		using (await deviceState.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (!deviceState.EmbeddedMonitors.TryGetValue(monitorId, out var monitorState)) throw new InvalidOperationException("Embedded monitor not found.");

			if (!await monitorState.SetBuiltInGraphicsAsync(graphicsId, cancellationToken).ConfigureAwait(false)) return;

			configuration = monitorState.CreatePersistedConfiguration();
		}

		await PersistConfigurationAsync(deviceState.EmbeddedMonitorConfigurationContainer, monitorId, configuration, cancellationToken).ConfigureAwait(false);
	}

	public async ValueTask SetImageAsync(Guid deviceId, Guid monitorId, UInt128 imageId, Rectangle imageRegion, CancellationToken cancellationToken)
	{
		if (!_embeddedMonitorDeviceStates.TryGetValue(deviceId, out var deviceState)) throw new InvalidOperationException("Device not found.");

		if ((uint)imageRegion.Left > ushort.MaxValue ||
			(uint)imageRegion.Top > ushort.MaxValue ||
			(uint)(imageRegion.Width - 1) > ushort.MaxValue - 1 ||
			(uint)(imageRegion.Height - 1) > ushort.MaxValue - 1)
		{
			throw new ArgumentException("Invalid crop region.");
		}

		PersistedMonitorConfiguration configuration;

		using (await deviceState.Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			if (!deviceState.EmbeddedMonitors.TryGetValue(monitorId, out var monitorState)) throw new InvalidOperationException("Embedded monitor not found.");

			if (!await monitorState.SetImageAsync(_imageStorageService, imageId, imageRegion, cancellationToken).ConfigureAwait(false)) return;

			configuration = monitorState.CreatePersistedConfiguration();
		}

		await PersistConfigurationAsync(deviceState.EmbeddedMonitorConfigurationContainer, monitorId, configuration, cancellationToken).ConfigureAwait(false);
	}

	public async IAsyncEnumerable<EmbeddedMonitorDeviceInformation> WatchDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateSingleWriterChannel<EmbeddedMonitorDeviceInformation>();

		var initialNotifications = new List<EmbeddedMonitorDeviceInformation>();
		using (await _lock.WaitAsync(cancellationToken))
		{
			foreach (var state in _embeddedMonitorDeviceStates.Values)
			{
				initialNotifications.Add(state.CreateInformation());
			}

			ArrayExtensions.InterlockedAdd(ref _deviceListeners, channel);
		}

		try
		{
			foreach (var notification in initialNotifications)
			{
				yield return notification;
			}
			initialNotifications = null;

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _deviceListeners, channel);
		}
	}

	public async IAsyncEnumerable<EmbeddedMonitorConfigurationWatchNotification> WatchConfigurationChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateSingleWriterChannel<EmbeddedMonitorConfigurationWatchNotification>();

		var initialNotifications = new List<EmbeddedMonitorConfigurationWatchNotification>();
		using (await _lock.WaitAsync(cancellationToken))
		{
			foreach (var device in _embeddedMonitorDeviceStates.Values)
			{
				using (await device.Lock.WaitAsync(cancellationToken))
				{
					foreach (var monitor in device.EmbeddedMonitors.Values)
					{
						initialNotifications.Add(monitor.CreateConfigurationNotification(device.Id));
					}
				}
			}

			ArrayExtensions.InterlockedAdd(ref _configurationChangeListeners, channel);
		}

		try
		{
			foreach (var notification in initialNotifications)
			{
				yield return notification;
			}
			initialNotifications = null;

			await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return notification;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _configurationChangeListeners, channel);
		}
	}

	private ValueTask PersistConfigurationAsync
	(
		IConfigurationContainer<Guid> embeddedMonitorConfigurationContainer,
		Guid embeddedMonitorId,
		PersistedMonitorConfiguration configuration,
		CancellationToken cancellationToken
	)
		=> embeddedMonitorConfigurationContainer.WriteValueAsync(embeddedMonitorId, configuration, cancellationToken);
}

public readonly struct EmbeddedMonitorDeviceInformation
{
	public required Guid DeviceId { get; init; }
	public required ImmutableArray<EmbeddedMonitorInformation> EmbeddedMonitors { get; init; }
}

public readonly struct EmbeddedMonitorInformation
{
	public required Guid MonitorId { get; init; }
	public required MonitorShape Shape { get; init; }
	public required Size ImageSize { get; init; }
	public required PixelFormat PixelFormat { get; init; }
	public required ImageFormats SupportedImageFormats { get; init; }
	public required EmbeddedMonitorCapabilities Capabilities { get; init; }
	public required ImmutableArray<EmbeddedMonitorGraphicsDescription> SupportedGraphics { get; init; }
}

public readonly struct EmbeddedMonitorConfigurationWatchNotification
{
	public required Guid DeviceId { get; init; }
	public required Guid MonitorId { get; init; }
	public required Guid GraphicsId { get; init; }
	public UInt128 ImageId { get; init; }
	public Rectangle ImageRegion { get; init; }
}
