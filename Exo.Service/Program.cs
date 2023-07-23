using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Exo.Service;

public class Program
{
	public static void Main(string[] args)
	{
		CreateHostBuilder(args).Build().Run();
	}

	public static IHostBuilder CreateHostBuilder(string[] args) =>
		Host.CreateDefaultBuilder(args)
			.UseWindowsService()
			.ConfigureWebHostDefaults
			(
				webBuilder => webBuilder.UseStartup<Startup>()
					.UseNamedPipes
					(
						o =>
						{
							var pipeSecurity = new PipeSecurity();
							// TODO: Fix for having better ACLs.
							pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null).Translate(typeof(NTAccount)), PipeAccessRights.FullControl, AccessControlType.Allow));
							//pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null).Translate(typeof(NTAccount)), PipeAccessRights.ReadData | PipeAccessRights.WriteData, AccessControlType.Allow));
							pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Translate(typeof(NTAccount)), PipeAccessRights.FullControl, AccessControlType.Allow));
							o.CurrentUserOnly = false;
							o.PipeSecurity = pipeSecurity;
						}
					)
					.ConfigureKestrel(o => o.ListenNamedPipe(@"Local\Exo.Service.Configuration", listenOptions => listenOptions.Protocols = HttpProtocols.Http2))
			)
			.UseSerilog((ctx, logger) => logger.ReadFrom.Configuration(ctx.Configuration));
}
