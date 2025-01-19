using System.Reflection;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Contracts.Ui.Overlay;
using Exo.Contracts.Ui.Settings;
using Exo.Discovery;
using Exo.I2C;
using Exo.Service.Grpc;
using Exo.Services;
using Exo.SystemManagementBus;
using Microsoft.Extensions.Hosting.WindowsServices;
using ProtoBuf.Grpc.Server;
using Serilog;

namespace Exo.Service;

public class Startup
{
	private const string RootConfigurationContainerKey = "root";

	public Startup(IHostEnvironment environment, IConfiguration configuration)
	{
		Environment = environment;
		Configuration = configuration;
	}

	public IHostEnvironment Environment { get; }
	public IConfiguration Configuration { get; }

	// This method gets called by the runtime. Use this method to add services to the container.
	public void ConfigureServices(IServiceCollection services)
	{
		string baseDirectory = Path.GetDirectoryName(typeof(Startup).Assembly.Location!)!;

		services.AddRazorPages();
		services.AddSingleton
		(
			// Remember: We use a Custom implementation of ServiceBase here…
			sp => sp.GetRequiredService<IHostLifetime>() is WindowsServiceLifetime windowsService ?
				windowsService.GetDeviceNotificationService() :
				new NotificationWindow()
		);
		services.AddSingleton
		(
			sp =>
			{
				var configurationService = new ConfigurationService(Path.Combine(baseDirectory, "cfg"));
				ConfigurationMigrationService.InitializeAsync(configurationService, Program.GitCommitId, default).GetAwaiter().GetResult();
				return configurationService;
			}
		);
		services.AddKeyedSingleton(RootConfigurationContainerKey, (sp, _) => sp.GetRequiredService<ConfigurationService>().GetRootContainer());
		services.AddKeyedSingleton(ConfigurationContainerNames.Devices, (sp, name) => sp.GetRequiredService<ConfigurationService>().GetContainer((string)name, GuidNameSerializer.Instance));
		services.AddKeyedSingleton(ConfigurationContainerNames.Discovery, (sp, name) => sp.GetRequiredService<ConfigurationService>().GetContainer((string)name));
		services.AddKeyedSingleton(ConfigurationContainerNames.DiscoveryFactory, (sp, name) => sp.GetRequiredKeyedService<IConfigurationContainer>(ConfigurationContainerNames.Discovery).GetContainer((string)name, GuidNameSerializer.Instance));
		services.AddKeyedSingleton(ConfigurationContainerNames.Assembly, (sp, name) => sp.GetRequiredService<ConfigurationService>().GetContainer((string)name, AssemblyNameSerializer.Instance));
		services.AddKeyedSingleton(ConfigurationContainerNames.CustomMenu, (sp, name) => sp.GetRequiredService<ConfigurationService>().GetContainer((string)name));
		services.AddKeyedSingleton(ConfigurationContainerNames.Lighting, (sp, name) => sp.GetRequiredService<ConfigurationService>().GetContainer((string)name));
		services.AddKeyedSingleton(ConfigurationContainerNames.Images, (sp, name) => sp.GetRequiredService<ConfigurationService>().GetContainer((string)name, ImageNameSerializer.Instance));
		if (Environment.IsDevelopment())
		{
			services.AddSingleton<IAssemblyDiscovery, DebugAssemblyDiscovery>();
		}
		else
		{
			services.AddSingleton<IAssemblyDiscovery, DynamicAssemblyDiscovery>();
		}
		services.AddSingleton
		(
			sp => AssemblyLoader.CreateAsync
			(
				sp.GetRequiredService<ILogger<AssemblyLoader>>(),
				sp.GetRequiredKeyedService<IConfigurationContainer>(RootConfigurationContainerKey),
				sp.GetRequiredService<IAssemblyDiscovery>(),
				default
			).GetAwaiter().GetResult()
		);
		services.AddSingleton<IAssemblyLoader>(sp => sp.GetRequiredService<AssemblyLoader>());
		services.AddSingleton<IMetadataSourceProvider>(sp => sp.GetRequiredService<AssemblyLoader>());
		services.AddSingleton
		(
			sp => DeviceRegistry.CreateAsync
			(
				sp.GetRequiredService<ILogger<DeviceRegistry>>(),
				sp.GetRequiredKeyedService<IConfigurationContainer<Guid>>(ConfigurationContainerNames.Devices),
				default
			).GetAwaiter().GetResult()
		);
		services.AddSingleton<IDriverRegistry>(sp => sp.GetRequiredService<DeviceRegistry>());
		services.AddSingleton<INestedDriverRegistryProvider>(sp => sp.GetRequiredService<DeviceRegistry>());
		services.AddSingleton<IDeviceWatcher>(sp => sp.GetRequiredService<DeviceRegistry>());
		services.AddSingleton<I2cBusRegistry>();
		services.AddSingleton<II2cBusRegistry>(sp => sp.GetRequiredService<I2cBusRegistry>());
		services.AddSingleton<II2cBusProvider>(sp => sp.GetRequiredService<I2cBusRegistry>());
		services.AddSingleton<SystemManagementBusRegistry>();
		services.AddSingleton<ISystemManagementBusRegistry>(sp => sp.GetRequiredService<SystemManagementBusRegistry>());
		services.AddSingleton<ISystemManagementBusProvider>(sp => sp.GetRequiredService<SystemManagementBusRegistry>());
		services.AddSingleton<LockedKeysWatcher>();
		services.AddSingleton<BacklightWatcher>();
		services.AddSingleton
		(
			sp => LightingEffectMetadataService.CreateAsync
			(
				sp.GetRequiredService<ILogger< LightingEffectMetadataService>>(),
				sp.GetRequiredKeyedService<IConfigurationContainer>(ConfigurationContainerNames.Lighting).GetContainer(ConfigurationContainerNames.LightingEffects, GuidNameSerializer.Instance),
				default
			).GetAwaiter().GetResult()
		);
		services.AddSingleton
		(
			sp => LightingService.CreateAsync
			(
				sp.GetRequiredService<ILogger<LightingService>>(),
				sp.GetRequiredKeyedService<IConfigurationContainer<Guid>>(ConfigurationContainerNames.Devices),
				sp.GetRequiredService<IDeviceWatcher>(),
				sp.GetRequiredService<LightingEffectMetadataService>(),
				default
			).GetAwaiter().GetResult()
		);
		services.AddSingleton
		(
			sp => PowerService.CreateAsync
			(
				sp.GetRequiredService<ILogger<PowerService>>(),
				sp.GetRequiredKeyedService<IConfigurationContainer<Guid>>(ConfigurationContainerNames.Devices),
				sp.GetRequiredService<IDeviceWatcher>(),
				sp.GetRequiredService<ChannelWriter<Programming.Event>>(),
				default
			).GetAwaiter().GetResult()
		);
		services.AddSingleton<KeyboardService>();
		services.AddSingleton<DisplayAdapterService>();
		services.AddSingleton<MotherboardService>();
		services.AddSingleton<MonitorService>();
		services.AddSingleton
		(
			sp => MouseService.CreateAsync
			(
				sp.GetRequiredService<ILogger<MouseService>>(),
				sp.GetRequiredKeyedService<IConfigurationContainer<Guid>>(ConfigurationContainerNames.Devices),
				sp.GetRequiredService<IDeviceWatcher>(),
				sp.GetRequiredService<ChannelWriter<Programming.Event>>(),
				default
			).GetAwaiter().GetResult()
		);
		services.AddSingleton
		(
			sp => SensorService.CreateAsync
			(
				sp.GetRequiredService<ILoggerFactory>(),
				sp.GetRequiredKeyedService<IConfigurationContainer<Guid>>(ConfigurationContainerNames.Devices),
				sp.GetRequiredService<IDeviceWatcher>(),
				default
			).GetAwaiter().GetResult()
		);
		services.AddSingleton
		(
			sp => CoolingService.CreateAsync
			(
				sp.GetRequiredService<ILoggerFactory>(),
				sp.GetRequiredKeyedService<IConfigurationContainer<Guid>>(ConfigurationContainerNames.Devices),
				sp.GetRequiredService<SensorService>(),
				sp.GetRequiredService<IDeviceWatcher>(),
				default
			).GetAwaiter().GetResult()
		);
		services.AddSingleton<ImageService>();
		services.AddSingleton
		(
			sp => ImageStorageService.CreateAsync
			(
				sp.GetRequiredService<ILogger<ImageStorageService>>(),
				sp.GetRequiredKeyedService<IConfigurationContainer<string>>(ConfigurationContainerNames.Images),
				Path.Combine(baseDirectory, "img"),
				default
			).GetAwaiter().GetResult()
		);
		services.AddSingleton
		(
			sp => CustomMenuService.CreateAsync
			(
				sp.GetRequiredService<ILogger<CustomMenuService>>(),
				sp.GetRequiredKeyedService<IConfigurationContainer>(ConfigurationContainerNames.CustomMenu),
				default
			).GetAwaiter().GetResult()
		);
		services.AddSingleton<OverlayNotificationService>();
		services.AddSingleton<ProgrammingService>();
		services.AddSingleton<EventQueue>();
		services.AddSingleton(sp => sp.GetRequiredService<EventQueue>().Reader);
		services.AddSingleton(sp => sp.GetRequiredService<EventQueue>().Writer);
		services.AddSingleton<IAssemblyParsedDataCache<DiscoveredAssemblyDetails>>
		(
			sp => PersistedAssemblyParsedDataCache.CreateAsync<DiscoveredAssemblyDetails>
			(
				sp.GetRequiredService<IAssemblyLoader>(),
				sp.GetRequiredKeyedService<IConfigurationContainer<AssemblyName>>(ConfigurationContainerNames.Assembly),
				default
			).GetAwaiter().GetResult()
		);
		// TODO: See if we want to keep this as a hosted service or not.
		services.AddSingleton
		(
			sp =>
			{
				return new DiscoveryOrchestrator
				(
					sp.GetRequiredService<ILogger<DiscoveryOrchestrator>>(),
					sp.GetRequiredService<IDriverRegistry>(),
					sp.GetRequiredService<IAssemblyParsedDataCache<DiscoveredAssemblyDetails>>(),
					sp.GetRequiredService<IAssemblyLoader>(),
					sp.GetRequiredKeyedService<IConfigurationContainer<Guid>>(ConfigurationContainerNames.DiscoveryFactory)
				);
			}
		);
		// The root discovery service must be pulled in as a hard dependency, as it will serve to bootstrap all other discovery services.
		services.AddSingleton
		(
			sp =>
			{
				var i2cProvider = sp.GetRequiredService<ProxiedI2cBusProvider>();
				return RootDiscoverySubsystem.CreateAsync
				(
					sp.GetRequiredService<ILoggerFactory>(),
					sp.GetRequiredService<INestedDriverRegistryProvider>(),
					sp.GetRequiredService<IDiscoveryOrchestrator>(),
					sp.GetRequiredService<IDeviceNotificationService>(),
					sp.GetRequiredService<II2cBusProvider>(),
					sp.GetRequiredService<ISystemManagementBusProvider>(),
					(deviceName) => new FallbackDisplayAdapterI2cBusProviderFeature(i2cProvider, deviceName)
				).GetAwaiter().GetResult();
			}
		);
		// Pull up the debug discovery service if we are building with support for fake devices
#if WITH_FAKE_DEVICES
		services.AddSingleton
		(
			sp => Debug.DebugDiscoverySystem.CreateAsync
			(
				sp.GetRequiredService<ILoggerFactory>(),
				sp.GetRequiredService<INestedDriverRegistryProvider>(),
				sp.GetRequiredService<IDiscoveryOrchestrator>()
			).GetAwaiter().GetResult()
		);
#endif
		services.AddSingleton<IDiscoveryOrchestrator>(sp => sp.GetRequiredService<DiscoveryOrchestrator>());
		services.AddHostedService(sp => sp.GetRequiredService<DiscoveryOrchestrator>());
		services.AddHostedService<CoreServices>();
		services.AddSingleton<GrpcMonitorControlProxyService>();
		services.AddSingleton(sp => new ReconnectingMonitorControlService(sp.GetRequiredService<GrpcMonitorControlProxyService>()));
		services.AddSingleton(sp => new ProxiedI2cBusProvider(sp.GetRequiredService<ReconnectingMonitorControlService>()));
		services.AddSingleton<GrpcDeviceService>();
		services.AddSingleton<GrpcPowerService>();
		services.AddSingleton<GrpcLightingService>();
		services.AddSingleton<GrpcImageService>();
		services.AddSingleton<GrpcSensorService>();
		services.AddSingleton<GrpcCoolingService>();
		services.AddSingleton<GrpcMouseService>();
		services.AddSingleton<GrpcMonitorService>();
		services.AddSingleton<GrpcProgrammingService>();
		services.AddSingleton<GrpcCustomMenuService>();
		services.AddSingleton(sp => new GrpcMetadataService(typeof(Startup).Assembly.Location, sp.GetRequiredService<IMetadataSourceProvider>()));
		services.AddSingleton<GrpcServiceLifetimeService>();
		services.AddSingleton<IOverlayCustomMenuService>(sp => sp.GetRequiredService<GrpcCustomMenuService>());
		services.AddSingleton<ISettingsCustomMenuService>(sp => sp.GetRequiredService<GrpcCustomMenuService>());
		services.AddCodeFirstGrpc(options => options.MaxReceiveMessageSize = 512 * 1024);
	}

	// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
	public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
	{
		if (env.IsDevelopment())
		{
			app.UseDeveloperExceptionPage();
		}
		else
		{
			app.UseExceptionHandler("/Error");
		}

		//app.UseStaticFiles();

		app.UseSerilogRequestLogging();

		app.UseRouting();

		app.UseAuthorization();

		var settingsEndpointFilter = new SettingsPipeFilter(@"Local\Exo.Service.Configuration");
		var overlayEndpointFilter = new SettingsPipeFilter(@"Local\Exo.Service.Overlay");
		var pipeEndpointFilter = new SettingsPipeFilter();

		app.UseEndpoints(endpoints =>
		{
			endpoints.MapGrpcService<GrpcDeviceService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcPowerService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcLightingService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcImageService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcSensorService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcCoolingService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcMouseService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcMonitorService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcProgrammingService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<ISettingsCustomMenuService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcMetadataService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcOverlayNotificationService>().AddEndpointFilter(overlayEndpointFilter);
			endpoints.MapGrpcService<GrpcMonitorControlProxyService>().AddEndpointFilter(overlayEndpointFilter);
			endpoints.MapGrpcService<IOverlayCustomMenuService>().AddEndpointFilter(overlayEndpointFilter);
			endpoints.MapGrpcService<GrpcServiceLifetimeService>().AddEndpointFilter(pipeEndpointFilter);
			endpoints.MapRazorPages();
		});
	}
}
