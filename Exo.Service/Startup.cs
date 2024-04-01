using System;
using System.Threading.Channels;
using Exo.Configuration;
using Exo.Contracts.Ui.Overlay;
using Exo.Contracts.Ui.Settings;
using Exo.Discovery;
using Exo.I2C;
using Exo.Service.Grpc;
using Exo.Services;
using Exo.SystemManagementBus;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Server;

namespace Exo.Service;

public class Startup
{
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
		services.AddRazorPages();
		services.AddSingleton
		(
			// Remember: We use a Custom implementation of ServiceBase here…
			sp => sp.GetRequiredService<IHostLifetime>() is WindowsServiceLifetime windowsService ?
				windowsService.GetDeviceNotificationService() :
				new NotificationWindow()
		);
		services.AddSingleton(sp => new ConfigurationService(Path.Combine(Path.GetDirectoryName(typeof(Startup).Assembly.Location!)!, "cfg")));
		if (Environment.IsDevelopment())
		{
			services.AddSingleton<IAssemblyDiscovery, DebugAssemblyDiscovery>();
		}
		else
		{
			services.AddSingleton<IAssemblyDiscovery, DynamicAssemblyDiscovery>();
		}
		services.AddSingleton<IAssemblyLoader, AssemblyLoader>();
		services.AddSingleton
		(
			sp => DeviceRegistry.CreateAsync
			(
				sp.GetRequiredService<ILogger<DeviceRegistry>>(),
				sp.GetRequiredService<ConfigurationService>().GetContainer("dev", GuidNameSerializer.Instance),
				default
			).GetAwaiter().GetResult()
		);
		services.AddSingleton<IDriverRegistry>(sp => sp.GetRequiredService<DeviceRegistry>());
		services.AddSingleton<INestedDriverRegistryProvider>(sp => sp.GetRequiredService<DeviceRegistry>());
		services.AddSingleton<IDeviceWatcher>(sp => sp.GetRequiredService<DeviceRegistry>());
		services.AddSingleton<I2CBusRegistry>();
		services.AddSingleton<II2CBusRegistry>(sp => sp.GetRequiredService<I2CBusRegistry>());
		services.AddSingleton<II2CBusProvider>(sp => sp.GetRequiredService<I2CBusRegistry>());
		services.AddSingleton<SystemManagementBusRegistry>();
		services.AddSingleton<ISystemManagementBusRegistry>(sp => sp.GetRequiredService<SystemManagementBusRegistry>());
		services.AddSingleton<ISystemManagementBusProvider>(sp => sp.GetRequiredService<SystemManagementBusRegistry>());
		services.AddSingleton<BatteryWatcher>();
		services.AddSingleton<DpiWatcher>();
		services.AddSingleton<LockedKeysWatcher>();
		services.AddSingleton<BacklightWatcher>();
		services.AddSingleton<LightingService>();
		services.AddSingleton<BatteryService>();
		services.AddSingleton<KeyboardService>();
		services.AddSingleton<DisplayAdapterService>();
		services.AddSingleton<MotherboardService>();
		services.AddSingleton<MonitorService>();
		services.AddSingleton<MouseService>();
		services.AddSingleton<ImageService>();
		services.AddSingleton<CustomMenuService>
		(
			sp => CustomMenuService.CreateAsync
			(
				sp.GetRequiredService<ILogger<CustomMenuService>>(),
				sp.GetRequiredService< ConfigurationService>().GetContainer("mnu"),
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
				sp.GetRequiredService<ConfigurationService>().GetContainer("asm", AssemblyNameSerializer.Instance),
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
					sp.GetRequiredService<ConfigurationService>().GetContainer("dcv").GetContainer("fac", GuidNameSerializer.Instance)
				);
			}
		);
		// The root discovery service must be pulled in as a hard dependency, as it will serve to bootstrap all other discovery services.
		services.AddSingleton<RootDiscoverySubsystem>
		(
			sp => RootDiscoverySubsystem.CreateAsync
			(
				sp.GetRequiredService<ILoggerFactory>(),
				sp.GetRequiredService<INestedDriverRegistryProvider>(),
				sp.GetRequiredService<IDiscoveryOrchestrator>(),
				sp.GetRequiredService<IDeviceNotificationService>(),
				sp.GetRequiredService<II2CBusProvider>(),
				sp.GetRequiredService<ISystemManagementBusProvider>()
			).GetAwaiter().GetResult()
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
		services.AddSingleton<GrpcDeviceService>();
		services.AddSingleton<GrpcLightingService>();
		services.AddSingleton<GrpcMouseService>();
		services.AddSingleton<GrpcMonitorService>();
		services.AddSingleton<GrpcProgrammingService>();
		services.AddSingleton<GrpcCustomMenuService>();
		services.AddSingleton<IOverlayCustomMenuService>(sp => sp.GetRequiredService<GrpcCustomMenuService>());
		services.AddSingleton<ISettingsCustomMenuService>(sp => sp.GetRequiredService<GrpcCustomMenuService>());
		services.AddCodeFirstGrpc();
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

		app.UseRouting();

		app.UseAuthorization();

		var settingsEndpointFilter = new SettingsPipeFilter(@"Local\Exo.Service.Configuration");
		var overlayEndpointFilter = new SettingsPipeFilter(@"Local\Exo.Service.Overlay");

		app.UseEndpoints(endpoints =>
		{
			endpoints.MapGrpcService<GrpcDeviceService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcLightingService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcMouseService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcMonitorService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcProgrammingService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<ISettingsCustomMenuService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcOverlayNotificationService>().AddEndpointFilter(overlayEndpointFilter);
			endpoints.MapGrpcService<IOverlayCustomMenuService>().AddEndpointFilter(overlayEndpointFilter);
			endpoints.MapRazorPages();
		});
	}
}
