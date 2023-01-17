using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Exo.Service
{
	public class Program
	{
		public static void Main(string[] args)
		{
			CreateHostBuilder(args).Build().Run();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.UseWindowsService()
				.ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())
				.UseSerilog((ctx, logger) => logger.ReadFrom.Configuration(ctx.Configuration));
	}
}
