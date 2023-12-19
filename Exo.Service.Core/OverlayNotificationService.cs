using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Exo.Overlay.Contracts;
using Exo.Programming.Annotations;

namespace Exo.Service;

[Module("Overlay")]
[TypeId(0x0FCB42F7, 0xAF51, 0x4C20, 0xA7, 0x1B, 0xDE, 0x0E, 0x8C, 0x3D, 0xCB, 0x23)]
public sealed class OverlayNotificationService
{
	private readonly DeviceRegistry _driverRegistry;
	private ChannelWriter<OverlayRequest>[]? _listeners;

	public OverlayNotificationService(DeviceRegistry driverRegistry) => _driverRegistry = driverRegistry;

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

	public void PostRequest(OverlayNotificationKind notificationKind, Guid deviceId = default, uint level = 0, uint maxLevel = 0, long value = 0)
	{
		if (Volatile.Read(ref _listeners) is { } listeners)
		{
			_driverRegistry.TryGetDeviceName(deviceId, out string? deviceName);

			listeners.TryWrite(new() { NotificationKind = notificationKind, DeviceName = deviceName, Level = level, MaxLevel = maxLevel, Value = value });
		}
	}
}
