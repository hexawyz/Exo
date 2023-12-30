using System;
using System.Threading.Channels;
using Exo.Service.Services;
using Exo.Services;
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
		services.AddSingleton(sp => DeviceRegistry.CreateAsync(sp.GetRequiredService<ConfigurationService>(), default).GetAwaiter().GetResult());
		services.AddSingleton<IDriverRegistry>(sp => sp.GetRequiredService<DeviceRegistry>());
		services.AddSingleton<IDeviceWatcher>(sp => sp.GetRequiredService<DeviceRegistry>());
		services.AddSingleton<BatteryWatcher>();
		services.AddSingleton<DpiWatcher>();
		services.AddSingleton<LockedKeysWatcher>();
		services.AddSingleton<BacklightWatcher>();
		services.AddSingleton<LightingService>();
		services.AddSingleton<BatteryService>();
		services.AddSingleton<KeyboardService>();
		services.AddSingleton<MouseService>();
		services.AddSingleton<ImageService>();
		services.AddSingleton<OverlayNotificationService>();
		services.AddSingleton<ProgrammingService>();
		services.AddSingleton<EventQueue>();
		services.AddSingleton(sp => sp.GetRequiredService<EventQueue>().Reader);
		services.AddSingleton(sp => sp.GetRequiredService<EventQueue>().Writer);
		services.AddSingleton<ISystemDeviceDriverRegistry, SystemDeviceDriverRegistry>();
		// NB: This will be refactored at some point, but this should probably not be a Hosted Service ?
		services.AddHostedService<HidDeviceManager>
		(
			sp =>
			{
				var assemblyLoader = sp.GetRequiredService<IAssemblyLoader>();
				return new HidDeviceManager
				(
					sp.GetRequiredService<ILoggerFactory>(),
					sp.GetRequiredService<ILogger<HidDeviceManager>>(),
					assemblyLoader,
					new AssemblyParsedDataCache<HidAssembyDetails>(assemblyLoader),
					sp.GetRequiredService<ISystemDeviceDriverRegistry>(),
					sp.GetRequiredService<IDriverRegistry>(),
					sp.GetRequiredService<IDeviceNotificationService>()
				);
			}
		);
		services.AddHostedService<CoreServices>();
		services.AddSingleton<GrpcDeviceService>();
		services.AddSingleton<GrpcLightingService>();
		services.AddSingleton<GrpcMouseService>();
		services.AddSingleton<GrpcProgrammingService>();
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

		app.UseStaticFiles();

		app.UseRouting();

		app.UseAuthorization();

		var settingsEndpointFilter = new SettingsPipeFilter(@"Local\Exo.Service.Configuration");
		var overlayEndpointFilter = new SettingsPipeFilter(@"Local\Exo.Service.Overlay");

		app.UseEndpoints(endpoints =>
		{
			endpoints.MapGrpcService<GrpcDeviceService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcLightingService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcMouseService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcProgrammingService>().AddEndpointFilter(settingsEndpointFilter);
			endpoints.MapGrpcService<GrpcOverlayNotificationService>().AddEndpointFilter(overlayEndpointFilter);
			endpoints.MapRazorPages();
		});
	}
}
