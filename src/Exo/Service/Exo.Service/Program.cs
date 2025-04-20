using System.Collections.Immutable;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Exo.Utils;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;

namespace Exo.Service;

public static class Program
{
	public static ImmutableArray<byte> GitCommitId => GitCommitHelper.GetCommitId(typeof(Program).Assembly);

	public static void Main(string[] args)
	{
		// Ensure that logs are written in the right place.
		// To be revisited later when the deployed file structure is better defined.
		Environment.SetEnvironmentVariable("LOGDIR", Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "logs")));

		SixLabors.ImageSharp.Configuration.Default.MemoryAllocator = new ImageSharpNativeMemoryAllocator();

		CreateHostBuilder(args).Build().Run();
	}

	public static IHostBuilder CreateHostBuilder(string[] args) =>
		Host.CreateDefaultBuilder(args)
			.UseWindowsService(
				o =>
				{
					o.CanHandlePowerEvent = true;
					o.CanHandleSessionChangeEvent = true;
					//o.CanHandleUserModeRebootEvent = true;
				}
			)
			.ConfigureWebHost
			(
				webBuilder => webBuilder.UseStartup<Startup>()
					.UseUrls()
					.UseKestrel()
					.UseNamedPipes()
					.ConfigureKestrel
					(
						// TODO: Remove
						o =>
						{
							o.ListenNamedPipe(@"Local\Exo.Service.IGNORE", listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
						}
					),
				o => { }
			)
			.UseSerilog((ctx, logger) => logger.ReadFrom.Configuration(ctx.Configuration));
}
