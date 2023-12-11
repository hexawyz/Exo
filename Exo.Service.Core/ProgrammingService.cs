using System.Threading.Channels;
using Exo.Features;
using Exo.Overlay.Contracts;
using Exo.Programming;

namespace Exo.Service;

public sealed class ProgrammingService : IAsyncDisposable
{
	private readonly ChannelReader<Event> _eventReader;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _runTask;

	// TODO:
	// - This implements hardcoded logic for now.
	// - All services should be plugged in as modules using RegisterModule<T>(), and the overlay notifications should be done via user-programmed code.
	// - A default program containing overlay programming should then be provided as a startup point for users to customize the logic.
	private readonly OverlayNotificationService _overlayNotificationService;
	private readonly Dictionary<Guid, Action<object?>> _hardcodedEventHandlers;

	// These hardcoded event handlers should be translated and included in the default program once the user-programing code logic is ready.
	private Dictionary<Guid, Action<object?>> CreateHardcodedEventHandlers()
	{
		return new()
		{
			// Caps Lock
			{
				KeyboardService.CapsLockOffEventGuid,
				p =>
				{
					var e = (DeviceEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.CapsLockOff, e.DeviceId);
				}
			},
			{
				KeyboardService.CapsLockOnEventGuid,
				p =>
				{
					var e = (DeviceEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.CapsLockOn, e.DeviceId);
				}
			},

			// Num Lock
			{
				KeyboardService.NumLockOffEventGuid,
				p =>
				{
					var e = (DeviceEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.NumLockOff, e.DeviceId);
				}
			},
			{
				KeyboardService.NumLockOnEventGuid,
				p =>
				{
					var e = (DeviceEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.NumLockOn, e.DeviceId);
				}
			},

			// Scroll Lock
			{
				KeyboardService.ScrollLockOffEventGuid,
				p =>
				{
					var e = (DeviceEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.ScrollLockOff, e.DeviceId);
				}
			},
			{
				KeyboardService.ScrollLockOnEventGuid,
				p =>
				{
					var e = (DeviceEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.ScrollLockOn, e.DeviceId);
				}
			},

			// Backlight
			{
				KeyboardService.KeyboardBacklightDownEventGuid,
				p =>
				{
					var e = (BacklightLevelEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.KeyboardBacklightDown, e.DeviceId, e.Level, e.MaximumLevel);
				}
			},
			{
				KeyboardService.KeyboardBacklightUpEventGuid,
				p =>
				{
					var e = (BacklightLevelEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.KeyboardBacklightUp, e.DeviceId, e.Level, e.MaximumLevel);
				}
			},
		};
	}

	public ProgrammingService(ChannelReader<Event> eventReader, OverlayNotificationService overlayNotificationService)
	{
		_eventReader = eventReader;
		_overlayNotificationService = overlayNotificationService;
		_hardcodedEventHandlers = CreateHardcodedEventHandlers();
		_cancellationTokenSource = new();
		_runTask = RunAsync(_cancellationTokenSource.Token);
	}


	public async ValueTask DisposeAsync()
	{
		if (_runTask.IsCompleted) return;

		_cancellationTokenSource.Cancel();
		_cancellationTokenSource.Dispose();
		await _runTask.ConfigureAwait(false);
	}

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		await foreach (var @event in _eventReader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
		{
			if (_hardcodedEventHandlers.TryGetValue(@event.EventId, out var handler))
			{
				handler(@event.Parameters);
			}
		}
	}

	public void RegisterModule<T>()
	{
	}

	public async IAsyncEnumerable<ModuleDefinition> GetModules()
	{
		yield break;
	}
}
