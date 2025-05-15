using System.ServiceProcess;
using Exo.Configuration;
using Exo.Discovery;
using Exo.Service.Ipc;
using Exo.Services;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace Exo.Service;

public class ExoService : ServiceBase
{
	private readonly ILoggerFactory _loggerFactory;
	private readonly ILogger<ExoService> _logger;

	private CancellationTokenSource? _cancellationTokenSource;
	private Task? _runTask;

	private readonly bool _isService;
	private readonly bool _isDevelopment;

	public ExoService()
	{
		_isDevelopment = Environment.GetEnvironmentVariable("EXO_LAUNCH_ENVIRONMENT") == "Development";
		_isService = WindowsServiceHelpers.IsWindowsService();

		if (_isService)
		{
			_loggerFactory = new LoggerFactory([new EventLogLoggerProvider(new EventLogSettings() { SourceName = "Exo" })], new LoggerFilterOptions() { MinLevel = LogLevel.Information });
		}
		else
		{
			var loggerConfiguration = new LoggerConfiguration()
				.Enrich.FromLogContext()
				.Enrich.WithMachineName()
				.Enrich.WithThreadId()
				.MinimumLevel.Is(_isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information)
				.WriteTo.File
				(
					path: Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "logs", "log.txt")),
					outputTemplate: @"[{Timestamp:HH:mm:ss}] [{Level}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
					rollingInterval: RollingInterval.Day,
					retainedFileCountLimit: 7,
					buffered: true
				);

			if (!_isService)
			{
				loggerConfiguration.WriteTo.Async(writeTo => writeTo.Console(outputTemplate: @"[{Timestamp:HH:mm:ss}] [{Level}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"));
			}

			var logger = loggerConfiguration.CreateLogger();

			_loggerFactory = new SerilogLoggerFactory(logger, true);
		}
		_logger = _loggerFactory.CreateLogger<ExoService>();
		_cancellationTokenSource = new();

		ServiceName = "Exo";
		CanHandlePowerEvent = true;
		CanHandleSessionChangeEvent = true;
		// TODO: Fix. The user mode reboot events seem to break the service.
		//CanHandleUserModeRebootEvent = true;
		CanShutdown = true;
		AutoLog = true;
	}

	public void Run()
	{
		if (_isService)
		{
			Run(this);
		}
		else
		{
			RunInline();
		}
	}

	private void RunInline()
	{
		using var window = new NotificationWindow();
		Console.CancelKeyPress += (s, e) =>
		{
			OnStop();
			Environment.Exit(0);
		};
		var tcs = new TaskCompletionSource();
		var startedTaskCompletionSource = new TaskCompletionSource();
		_runTask = RunAsync(tcs.Task, startedTaskCompletionSource, window, window, _cancellationTokenSource!.Token);
		tcs.SetResult();
		startedTaskCompletionSource.Task.GetAwaiter().GetResult();
		_runTask.GetAwaiter().GetResult();
	}

	protected override void OnStart(string[] args)
	{
		_logger.Log(LogLevel.Information, "Starting…");
		if (_runTask is not null || _cancellationTokenSource is null) throw new InvalidOperationException();
		var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var startedTaskCompletionSource = new TaskCompletionSource();
		_runTask = RunAsync(tcs.Task, startedTaskCompletionSource, GetDeviceNotificationService(), GetPowerNotificationService(), _cancellationTokenSource.Token);
		tcs.SetResult();
		startedTaskCompletionSource.Task.GetAwaiter().GetResult();
	}

	protected override void OnStop()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			_logger.Log(LogLevel.Information, "Stopping…");
			try
			{
				cts.Cancel();
				_runTask?.GetAwaiter().GetResult();
			}
			finally
			{
				_loggerFactory.Dispose();
			}
		}
	}

	// TODO: Try to parallelize some of the initialization
	private async Task RunAsync(Task start, TaskCompletionSource startedTaskCompletionSource, IDeviceNotificationService deviceNotificationService, IPowerNotificationService powerNotificationService, CancellationToken cancellationToken)
	{
		await start.ConfigureAwait(false);
		start = null!;

		string baseDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location!)!;

		try
		{
			var configurationService = new ConfigurationService(Path.Combine(baseDirectory, "cfg"));
			await ConfigurationMigrationService.InitializeAsync(configurationService, Program.GitCommitId, default).ConfigureAwait(false);

			var rootConfigurationContainer = configurationService.GetRootContainer();
			var devicesConfigurationContainer = configurationService.GetContainer(ConfigurationContainerNames.Devices, GuidNameSerializer.Instance);
			var discoveryConfigurationContainer = configurationService.GetContainer(ConfigurationContainerNames.Discovery);
			var discoveryFactoryConfigurationContainer = discoveryConfigurationContainer.GetContainer(ConfigurationContainerNames.DiscoveryFactory, GuidNameSerializer.Instance);
			var assemblyConfigurationContainer = configurationService.GetContainer(ConfigurationContainerNames.Assembly, AssemblyNameSerializer.Instance);
			var customMenuConfigurationContainer = configurationService.GetContainer(ConfigurationContainerNames.CustomMenu);
			var lightingConfigurationContainer = configurationService.GetContainer(ConfigurationContainerNames.Lighting);
			var imagesConfigurationContainer = configurationService.GetContainer(ConfigurationContainerNames.Images, ImageNameSerializer.Instance);

			IAssemblyDiscovery assemblyDiscovery = _isDevelopment ?
				new DebugAssemblyDiscovery() :
				new DynamicAssemblyDiscovery(baseDirectory);

			using var assemblyLoader = await AssemblyLoader.CreateAsync
			(
				_loggerFactory.CreateLogger<AssemblyLoader>(),
				rootConfigurationContainer,
				assemblyDiscovery,
				typeof(ExoService).Assembly.Location,
				cancellationToken
			).ConfigureAwait(false);

			using var deviceRegistry = await DeviceRegistry.CreateAsync
			(
				_loggerFactory.CreateLogger<DeviceRegistry>(),
				devicesConfigurationContainer,
				cancellationToken
			).ConfigureAwait(false);

			var i2cBusRegistry = new I2cBusRegistry();
			using var smBusRegistry = new SystemManagementBusRegistry();

			await using var lockedKeysWatcher = new LockedKeysWatcher(deviceRegistry);
			await using var backlightWatcher = new BacklightWatcher(deviceRegistry);

			await using var lightingEffectMetadataService = await LightingEffectMetadataService.CreateAsync
			(
				_loggerFactory.CreateLogger<LightingEffectMetadataService>(),
				lightingConfigurationContainer.GetContainer(ConfigurationContainerNames.LightingEffects, GuidNameSerializer.Instance),
				cancellationToken
			).ConfigureAwait(false);
			var eventQueue = new EventQueue();
			await using var lightingService = await LightingService.CreateAsync
			(
				_loggerFactory.CreateLogger<LightingService>(),
				lightingConfigurationContainer,
				devicesConfigurationContainer,
				deviceRegistry,
				powerNotificationService,
				cancellationToken
			).ConfigureAwait(false);
			await using var lightService = await LightService.CreateAsync
			(
				_loggerFactory.CreateLogger<LightService>(),
				devicesConfigurationContainer,
				deviceRegistry,
				cancellationToken
			).ConfigureAwait(false);
			var imageService = new ImageService();
			await using var imageStorageService = await ImageStorageService.CreateAsync
			(
				_loggerFactory.CreateLogger<ImageStorageService>(),
				imagesConfigurationContainer,
				Path.Combine(baseDirectory, "img"),
				cancellationToken
			).ConfigureAwait(false);
			await using var embeddedMonitorService = await EmbeddedMonitorService.CreateAsync
			(
				_loggerFactory.CreateLogger<EmbeddedMonitorService>(),
				devicesConfigurationContainer,
				deviceRegistry,
				imageStorageService,
				cancellationToken
			).ConfigureAwait(false);
			await using var powerService = await PowerService.CreateAsync
			(
				_loggerFactory.CreateLogger<PowerService>(),
				devicesConfigurationContainer,
				deviceRegistry,
				eventQueue.Writer,
				cancellationToken
			).ConfigureAwait(false);
			await using var keyboardService = new KeyboardService(lockedKeysWatcher, backlightWatcher, eventQueue.Writer);
			await using var displayAdapterService = new DisplayAdapterService(deviceRegistry, i2cBusRegistry);
			await using var motherboardService = new MotherboardService(deviceRegistry, smBusRegistry);
			await using var monitorService = new MonitorService(_loggerFactory.CreateLogger<MonitorService>(), deviceRegistry);
			await using var mouseService = await MouseService.CreateAsync
			(
				_loggerFactory.CreateLogger<MouseService>(),
				devicesConfigurationContainer,
				deviceRegistry,
				eventQueue.Writer,
				cancellationToken
			).ConfigureAwait(false);
			await using var sensorService = await SensorService.CreateAsync
			(
				_loggerFactory,
				devicesConfigurationContainer,
				deviceRegistry,
				cancellationToken
			).ConfigureAwait(false);
			await using var coolingService = await CoolingService.CreateAsync
			(
				_loggerFactory,
				devicesConfigurationContainer,
				sensorService,
				deviceRegistry,
				cancellationToken
			).ConfigureAwait(false);
			var customMenuService = await CustomMenuService.CreateAsync
			(
				_loggerFactory.CreateLogger<CustomMenuService>(),
				customMenuConfigurationContainer,
				cancellationToken
			).ConfigureAwait(false);
			var overlayNotificationService = new OverlayNotificationService(deviceRegistry);
			var programmingService = new ProgrammingService(eventQueue.Reader, overlayNotificationService);
			await using var assemblyDetailsCache = await PersistedAssemblyParsedDataCache.CreateAsync<DiscoveredAssemblyDetails>
			(
				assemblyLoader,
				assemblyConfigurationContainer,
				cancellationToken
			).ConfigureAwait(false);
			// TODO: See if we want to keep this as a hosted service or not.
			var discoveryOrchestrator = new DiscoveryOrchestrator
			(
				_loggerFactory.CreateLogger<DiscoveryOrchestrator>(),
				deviceRegistry,
				assemblyDetailsCache,
				assemblyLoader,
				discoveryFactoryConfigurationContainer
			);
			var monitorControlProxyService = new MonitorControlProxyService();
			var reconnectingMonitorControlService = new ReconnectingMonitorControlService(monitorControlProxyService);
			var proxiedI2cBusProvider = new ProxiedI2cBusProvider(reconnectingMonitorControlService);

			// The root discovery service must be pulled in as a hard dependency, as it will serve to bootstrap all other discovery services.
			await using var rootDiscoverySubsystem = await RootDiscoverySubsystem.CreateAsync
			(
				_loggerFactory,
				deviceRegistry,
				discoveryOrchestrator,
				deviceNotificationService,
				powerNotificationService,
				i2cBusRegistry,
				smBusRegistry,
				(deviceName) => new FallbackDisplayAdapterI2cBusProviderFeature(proxiedI2cBusProvider, deviceName)
			).ConfigureAwait(false);
			// Pull up the debug discovery service if we are building with support for fake devices
#if WITH_FAKE_DEVICES
			await using var debugDiscoverySystem = await Debug.DebugDiscoverySystem.CreateAsync
			(
				_loggerFactory,
				deviceRegistry,
				discoveryOrchestrator
			).ConfigureAwait(false);
#endif

			var helperIpcService = new HelperIpcService
			(
				_loggerFactory.CreateLogger<HelperPipeServerConnection>(),
				overlayNotificationService,
				customMenuService,
				monitorControlProxyService
			);
			var uiIpcService = new UiIpcService
			(
				_loggerFactory.CreateLogger<UiPipeServerConnection>(),
				assemblyLoader,
				customMenuService,
				programmingService,
				imageStorageService,
				deviceRegistry,
				powerService,
				mouseService,
				monitorService,
				sensorService,
				coolingService,
				lightingEffectMetadataService,
				lightingService,
				embeddedMonitorService,
				lightService
			);

			programmingService.RegisterModule(lightingService);
			programmingService.RegisterModule(lightService);
			programmingService.RegisterModule(embeddedMonitorService);
			programmingService.RegisterModule(powerService);
			programmingService.RegisterModule(keyboardService);
			programmingService.RegisterModule(mouseService);
			programmingService.RegisterModule(imageService);
			programmingService.RegisterModule(overlayNotificationService);

			await uiIpcService.StartAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				await helperIpcService.StartAsync(cancellationToken).ConfigureAwait(false);
				try
				{
					await discoveryOrchestrator.StartAsync(cancellationToken).ConfigureAwait(false);
					try
					{
						try
						{
							startedTaskCompletionSource.TrySetResult();
							startedTaskCompletionSource = null!;
							await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
						}
						catch (OperationCanceledException)
						{
						}
					}
					finally
					{
						await discoveryOrchestrator.StopAsync(default).ConfigureAwait(false);
					}
				}
				finally
				{
					await helperIpcService.StopAsync(default).ConfigureAwait(false);
				}
			}
			finally
			{
				await uiIpcService.StopAsync(default).ConfigureAwait(false);
			}
		}
		catch (Exception ex) when (startedTaskCompletionSource is not null)
		{
			startedTaskCompletionSource.TrySetException(ex);
		}
		catch (OperationCanceledException)
		{
		}
	}
}
