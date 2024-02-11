using Exo.Discovery;

namespace Exo.Service;

// This is only used to pull a dependency on core services.
// Because we need the core services to be started even if they are not requested for some reason.
// This seems simpler than making all those services hosted services.
public sealed class CoreServices : IHostedService
{
	public CoreServices
	(
		RootDiscoverySubsystem rootDiscoverySubsystem,
		LightingService lightingService,
		BatteryService batteryService,
		KeyboardService keyboardService,
		MouseService mouseService,
		ImageService imageService,
		OverlayNotificationService overlayNotificationService,
		ProgrammingService programmingService
	)
	{
		programmingService.RegisterModule(lightingService);
		programmingService.RegisterModule(batteryService);
		programmingService.RegisterModule(keyboardService);
		programmingService.RegisterModule(mouseService);
		programmingService.RegisterModule(imageService);
		programmingService.RegisterModule(overlayNotificationService);
	}

	public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
