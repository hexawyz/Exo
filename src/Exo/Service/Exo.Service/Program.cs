using System.Collections.Immutable;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Exo.Contracts.Ui;
using Exo.Programming;
using Exo.Utils;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using ProtoBuf.Meta;
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

		var runtimeTypeModel = RuntimeTypeModel.Default;

		runtimeTypeModel.Add<UInt128>(false).SerializerType = typeof(UInt128Serializer);

		foreach (var type in typeof(NamedElement).Assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(NamedElement))))
		{
			var metaType = runtimeTypeModel[type];

			metaType.Add(1, nameof(NamedElement.Id));
			metaType.Add(2, nameof(NamedElement.Name));
			metaType.Add(3, nameof(NamedElement.Comment));
		}

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
					.ConfigureKestrel
					(
						o =>
						{
							o.ListenNamedPipe(@"Local\Exo.Service.Configuration", listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
						}
					),
				o => { }
			)
			.UseSerilog((ctx, logger) => logger.ReadFrom.Configuration(ctx.Configuration));
}
