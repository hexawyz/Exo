using Exo.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;

namespace Exo.Service;

public class Startup
{
	public Startup(IConfiguration configuration)
	{
		Configuration = configuration;
	}

	public IConfiguration Configuration { get; }

	// This method gets called by the runtime. Use this method to add services to the container.
	public void ConfigureServices(IServiceCollection services)
	{
		services.AddRazorPages();
		services.AddSingleton
		(
			// Remember: We use a Cutom implementation of ServiceBase here…
			sp => sp.GetRequiredService<IHostLifetime>() is WindowsServiceLifetime windowsService ?
				windowsService.GetDeviceNotificationService() :
				new NotificationWindow()
		);
		services.AddSingleton<IAssemblyDiscovery, DebugAssemblyDiscovery>();
		services.AddSingleton<IAssemblyLoader, AssemblyLoader>();
		services.AddSingleton<DriverRegistry>();
		services.AddSingleton<ISystemDeviceDriverRegistry, SystemDeviceDriverRegistry>();
		services.AddHostedService<HidDeviceManager>
		(
			sp =>
			{
				var assemblyLoader = sp.GetRequiredService<IAssemblyLoader>();
				return new HidDeviceManager
				(
					sp.GetRequiredService<ILogger<HidDeviceManager>>(),
					assemblyLoader,
					new AssemblyParsedDataCache<HidAssembyDetails>(assemblyLoader),
					sp.GetRequiredService<ISystemDeviceDriverRegistry>(),
					sp.GetRequiredService<DriverRegistry>(),
					sp.GetRequiredService<IDeviceNotificationService>()
				);
			}
		);
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

		app.UseEndpoints(endpoints =>
		{
			endpoints.MapRazorPages();
		});
	}
}
