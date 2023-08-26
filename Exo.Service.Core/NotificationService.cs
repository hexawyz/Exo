using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Exo.Overlay.Contracts;

namespace Exo.Service;

public sealed class OverlayNotificationService
{
	private readonly DriverRegistry _driverRegistry;
	private ChannelWriter<OverlayRequest>[]? _listeners;

	public OverlayNotificationService(DriverRegistry driverRegistry) => _driverRegistry = driverRegistry;

	public async IAsyncEnumerable<OverlayRequest> WatchOverlayRequestsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Watcher.CreateChannel<OverlayRequest>();

		ArrayExtensions.InterlockedAdd(ref _listeners, channel);
		try
		{
			await foreach (var request in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return request;
			}
		}
		finally
		{
			ArrayExtensions.InterlockedRemove(ref _listeners, channel);
		}
	}

	public void PostRequest(OverlayNotificationKind notificationKind, Guid deviceId = default, uint level = 0, uint maxLevel = 0)
	{
		if (Volatile.Read(ref _listeners) is { } listeners)
		{
			_driverRegistry.TryGetDeviceName(deviceId, out string? deviceName);

			listeners.TryWrite(new() { NotificationKind = notificationKind, DeviceName = deviceName, Level = level, MaxLevel = maxLevel });
		}
	}
}
