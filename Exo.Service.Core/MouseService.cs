using System.Threading.Channels;
using Exo.Programming;
using Exo.Programming.Annotations;
using Exo.Service.Events;
using Exo.Service.Services;

namespace Exo.Service;

[Module("Mouse")]
[TypeId(0x397BD522, 0x0E19, 0x4932, 0xBE, 0x80, 0x06, 0xB7, 0x8E, 0x17, 0x2A, 0x64)]
[Event<DeviceEventParameters>("DpiDown", 0xCCCCDEE1, 0x5E77, 0x4DB9, 0x8E, 0x10, 0x3A, 0x82, 0x89, 0x9A, 0xE8, 0x66)]
[Event<DeviceEventParameters>("DpiUp", 0xD40A9183, 0xA9BB, 0x4EDF, 0x93, 0x78, 0x66, 0x90, 0xF2, 0x28, 0x11, 0x9B)]
public sealed class MouseService
{
	public static readonly Guid DpiDownEventGuid = new(0xCCCCDEE1, 0x5E77, 0x4DB9, 0x8E, 0x10, 0x3A, 0x82, 0x89, 0x9A, 0xE8, 0x66);
	public static readonly Guid DpiUpEventGuid = new(0xD40A9183, 0xA9BB, 0x4EDF, 0x93, 0x78, 0x66, 0x90, 0xF2, 0x28, 0x11, 0x9B);

	private readonly DpiWatcher _dpiWatcher;

	private readonly ChannelWriter<Event> _eventWriter;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _dpiWatchTask;

	public MouseService(DpiWatcher dpiWatcher, ChannelWriter<Event> eventWriter)
	{
		_dpiWatcher = dpiWatcher;
		_eventWriter = eventWriter;
		_cancellationTokenSource = new();
		_dpiWatchTask = WatchDpiChangesAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (_dpiWatchTask.IsCompleted) return;

		_cancellationTokenSource.Cancel();
		await _dpiWatchTask.ConfigureAwait(false);
		_cancellationTokenSource.Dispose();
	}

	private async Task WatchPresetsAsync(CancellationToken cancellationToken)
	{
	}

	private async Task WatchDpiChangesAsync(CancellationToken cancellationToken)
	{
		await foreach (var notification in _dpiWatcher.WatchAsync(cancellationToken))
		{
			if (notification.NotificationKind == WatchNotificationKind.Update)
			{
				int? status = null;

				if (notification.OldValue.PresetIndex is not null && notification.NewValue.PresetIndex is not null)
				{
					status = Comparer<byte>.Default.Compare(notification.NewValue.PresetIndex.GetValueOrDefault(), notification.OldValue.PresetIndex.GetValueOrDefault());
				}
				else
				{
					int h = Comparer<int>.Default.Compare(notification.NewValue.Dpi.Horizontal, notification.OldValue.Dpi.Horizontal);
					int v = Comparer<int>.Default.Compare(notification.NewValue.Dpi.Vertical, notification.OldValue.Dpi.Vertical);

					if (Math.Sign(h) == Math.Sign(v))
					{
						status = h;
					}
				}

				if (status is not null and not 0)
				{
					_eventWriter.TryWrite
					(
						Event.Create
						(
							status >= 0 ? DpiUpEventGuid : DpiDownEventGuid,
							new MouseDpiEventParameters
							(
								(DeviceId)notification.DeviceId,
								notification.NewValue.Dpi.Horizontal,
								notification.NewValue.Dpi.Vertical,
								checked((byte)notification.Presets.Length),
								notification.NewValue.PresetIndex
							)
						)
					);
				}
			}
		}
	}
}
