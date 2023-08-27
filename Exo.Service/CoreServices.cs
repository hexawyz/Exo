namespace Exo.Service;

// This is only used to pull a dependency on core services.
// Because we need the core services to be started even if they are not requested for some reason.
// This seems simpler than making all those services hosted services.
public sealed class CoreServices : IHostedService
{
	public CoreServices
	(
		LightingService lightingService,
		BatteryService batteryService,
		KeyboardService keyboardService,
		OverlayNotificationService overlayNotificationService
	) { }

	public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
