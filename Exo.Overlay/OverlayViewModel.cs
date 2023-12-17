using System.Threading.Channels;
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
	private readonly Channel<OverlayContentViewModel> _overlayChannel;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;
	private readonly Task _updateTask;

	public OverlayViewModel()
	{
		_connectionManager = new("Local\\Exo.Service.Overlay");
		_overlayNotificationService = _connectionManager.Channel.CreateGrpcService<IOverlayNotificationService>();
		_overlayChannel = Channel.CreateUnbounded<OverlayContentViewModel>(new() { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = true });
		_cancellationTokenSource = new();
		_watchTask = WatchAsync(_cancellationTokenSource.Token);
		_updateTask = ProcessOverlayUpdatesAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (_watchTask.IsCompleted) return;

		_cancellationTokenSource.Cancel();
		await _watchTask.ConfigureAwait(false);
		await _updateTask.ConfigureAwait(false);
		_cancellationTokenSource.Dispose();
	}

	private async Task WatchAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var request in _overlayNotificationService.WatchOverlayRequestsAsync(cancellationToken))
			{
				OverlayContentViewModel? content = null;
				switch (request.NotificationKind)
				{
				case OverlayNotificationKind.Custom:
					break;
				case OverlayNotificationKind.CapsLockOff:
					content = new("\uE84A", request.DeviceName);
					break;
				case OverlayNotificationKind.CapsLockOn:
					content = new("\uE84B", request.DeviceName);
					break;
				case OverlayNotificationKind.NumLockOff:
				case OverlayNotificationKind.NumLockOn:
					break;
				case OverlayNotificationKind.ScrollLockOff:
				case OverlayNotificationKind.ScrollLockOn:
					break;
				case OverlayNotificationKind.FnLockOff:
					content = new("\uE785", request.DeviceName);
					break;
				case OverlayNotificationKind.FnLockOn:
					content = new("\uE72E", request.DeviceName);
					break;
				case OverlayNotificationKind.MonitorBrightnessDown:
					content = new("\uEC8A", request.DeviceName, (int)request.Level, (int)request.MaxLevel);
					break;
				case OverlayNotificationKind.MonitorBrightnessUp:
					content = new("\uE706", request.DeviceName, (int)request.Level, (int)request.MaxLevel);
					break;
				case OverlayNotificationKind.KeyboardBacklightDown:
					content = new("\uED3A", request.DeviceName, (int)request.Level, (int)request.MaxLevel);
					break;
				case OverlayNotificationKind.KeyboardBacklightUp:
					content = new("\uED39", request.DeviceName, (int)request.Level, (int)request.MaxLevel);
					break;
				case OverlayNotificationKind.BatteryLow:
					content = new(GetBatteryDischargingGlyph(request.MaxLevel == 10 ? request.Level : 1), request.DeviceName);
					break;
				case OverlayNotificationKind.BatteryFullyCharged:
					content = new(BatteryChargingGlyphs[10], request.DeviceName);
					break;
				case OverlayNotificationKind.BatteryExternalPowerDisconnected:
					// TODO: Better glyph when charge status is unknown.
					content = new(request.MaxLevel == 10 ? GetBatteryDischargingGlyph(request.Level) : "\U0001F6C7\uFE0F", request.DeviceName);
					break;
				case OverlayNotificationKind.BatteryExternalPowerConnected:
					content = new(request.MaxLevel == 10 ? GetBatteryChargingGlyph(request.Level) : "\uE945", request.DeviceName);
					break;
				case OverlayNotificationKind.MouseDpiDown:
					content = new("\uF08E", request.DeviceName, (int)request.Level, (int)request.MaxLevel);
					break;
				case OverlayNotificationKind.MouseDpiUp:
					content = new("\uF090", request.DeviceName, (int)request.Level, (int)request.MaxLevel);
					break;
				default:
					continue;
				}
				if (content is not null)
				{
					_overlayChannel.Writer.TryWrite(content);
				}
			}
		}
		catch (OperationCanceledException)
		{
			_overlayChannel.Writer.TryComplete();
		}
	}

	private async Task ProcessOverlayUpdatesAsync(CancellationToken cancellationToken)
	{
		var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var cancelTask = Task.CompletedTask;

		await foreach (var overlay in _overlayChannel.Reader.ReadAllAsync(cancellationToken))
		{
			if (!cancelTask.IsCompleted)
			{
				cts.Cancel();
				cts.Dispose();
				await cancelTask;
				cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			}

			Content = overlay;
			IsVisible = true;
			cancelTask = DelayedCancelOverlayAsync(3_000, cts.Token);
		}
	}

	private async Task DelayedCancelOverlayAsync(int delay, CancellationToken cancellationToken)
	{
		try
		{
			await Task.Delay(delay, cancellationToken);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		if (cancellationToken.IsCancellationRequested) return;
		IsVisible = false;
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
