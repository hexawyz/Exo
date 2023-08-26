using Exo.Overlay.Contracts;
using Exo.Ui;
using ProtoBuf.Grpc.Client;

namespace Exo.Overlay;

internal class OverlayViewModel : BindableObject, IAsyncDisposable
{
	private static readonly string[] BatteryDischargingGlyphs = new[]
	{
		"\uEBA0",
		"\uEBA1",
		"\uEBA2",
		"\uEBA3",
		"\uEBA4",
		"\uEBA5",
		"\uEBA6",
		"\uEBA7",
		"\uEBA8",
		"\uEBA9",
		"\uEBAA",
	};

	private static readonly string[] BatteryChargingGlyphs = new[]
	{
		"\uEBAB",
		"\uEBAC",
		"\uEBAD",
		"\uEBAE",
		"\uEBAF",
		"\uEBB0",
		"\uEBB1",
		"\uEBB2",
		"\uEBB3",
		"\uEBB4",
		"\uEBB5",
	};

	private static string GetBatteryDischargingGlyph(uint level)
	{
		if (level > 10) return BatteryDischargingGlyphs[10];
		return BatteryDischargingGlyphs[level];
	}

	private static string GetBatteryChargingGlyph(uint level)
	{
		if (level > 10) return BatteryChargingGlyphs[10];
		return BatteryChargingGlyphs[level];
	}

	private readonly ServiceConnectionManager _connectionManager;
	private readonly IOverlayNotificationService _overlayNotificationService;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;

	public OverlayViewModel()
	{
		_connectionManager = new("Local\\Exo.Service.Overlay");
		_overlayNotificationService = _connectionManager.Channel.CreateGrpcService<IOverlayNotificationService>();
		_cancellationTokenSource = new();
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (_watchTask.IsCompleted) return;

		_cancellationTokenSource.Cancel();
		await _watchTask.ConfigureAwait(false);
		_cancellationTokenSource.Dispose();
	}

	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var request in _overlayNotificationService.WatchOverlayRequestsAsync(cancellationToken))
			{
				switch (request.NotificationKind)
				{
				case OverlayNotificationKind.Custom:
					break;
				case OverlayNotificationKind.CapsLockOff:
				case OverlayNotificationKind.CapsLockOn:
					break;
				case OverlayNotificationKind.NumLockOff:
				case OverlayNotificationKind.NumLockOn:
					break;
				case OverlayNotificationKind.ScrollLockOff:
				case OverlayNotificationKind.ScrollLockOn:
					break;
				case OverlayNotificationKind.FnLockOff:
					Content = new("\uE785", request.DeviceName);
					break;
				case OverlayNotificationKind.FnLockOn:
					Content = new("\uE72E", request.DeviceName);
					break;
				case OverlayNotificationKind.MonitorBrightnessDown:
					Content = new("\uEC8A", request.DeviceName, (int)request.Level, (int)request.MaxLevel);
					break;
				case OverlayNotificationKind.MonitorBrightnessUp:
					Content = new("\uE706", request.DeviceName, (int)request.Level, (int)request.MaxLevel);
					break;
				case OverlayNotificationKind.KeyboardBacklightDown:
					Content = new("\uED3A", request.DeviceName, (int)request.Level, (int)request.MaxLevel);
					break;
				case OverlayNotificationKind.KeyboardBacklightUp:
					Content = new("\uED39", request.DeviceName, (int)request.Level, (int)request.MaxLevel);
					break;
				case OverlayNotificationKind.BatteryLow:
					Content = new(GetBatteryDischargingGlyph(request.MaxLevel == 10 ? request.Level : 1), request.DeviceName);
					break;
				case OverlayNotificationKind.BatteryFullyCharged:
					Content = new(BatteryChargingGlyphs[10], request.DeviceName);
					break;
				case OverlayNotificationKind.BatteryExternalPowerDisconnected:
					// TODO: Better glyph when charge status is unknown.
					Content = new(request.MaxLevel == 10 ? GetBatteryDischargingGlyph(request.Level) : "\U0001F6C7\uFE0F", request.DeviceName);
					break;
				case OverlayNotificationKind.BatteryExternalPowerConnected:
					// TODO: Change the unknown level glyph to be coherent with the disconnected status.
					Content = new(request.MaxLevel == 10 ? GetBatteryChargingGlyph(request.Level) : "\U0001F50C\uFE0F", request.DeviceName);
					break;
				default:
					continue;
				}
				// TODO: Handle notification override => Push to a channel and handle the timer dynamically.
				IsVisible = true;
				await Task.Delay(3_000);
				IsVisible = false;
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private OverlayContentViewModel? _content;
	public OverlayContentViewModel? Content
	{
		get => _content;
		set => SetValue(ref _content, value, ChangedProperty.Content);
	}

	private bool _isVisible;
	public bool IsVisible
	{
		get => _isVisible;
		set => SetValue(ref _isVisible, value, ChangedProperty.IsVisible);
	}
}
