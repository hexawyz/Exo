using System.Threading.Channels;
using Exo.Service;
using Exo.Ui;

namespace Exo.Overlay;

internal sealed class OverlayViewModel : BindableObject, IAsyncDisposable
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

	private readonly ChannelReader<OverlayRequest> _requestReader;
	private readonly Channel<OverlayContentViewModel> _overlayChannel;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _watchTask;
	private readonly Task _updateTask;

	public OverlayViewModel(ChannelReader<OverlayRequest> requestReader)
	{
		_requestReader = requestReader;
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
			await foreach (var request in _requestReader.ReadAllAsync(cancellationToken))
			{
				OverlayContentViewModel? content = null;
				switch (request.NotificationKind)
				{
				case OverlayNotificationKind.Custom:
					break;
				case OverlayNotificationKind.CapsLockOff:
					content = new() { Font = GlyphFont.FluentSystemIcons, Glyph = "\uE975", Description = request.DeviceName };
					break;
				case OverlayNotificationKind.CapsLockOn:
					content = new() { Font = GlyphFont.FluentSystemIcons, Glyph = "\uE974", Description = request.DeviceName };
					break;
				case OverlayNotificationKind.NumLockOff:
					content = new() { Font = GlyphFont.FluentSystemIcons, Glyph = "\uEB65", Description = request.DeviceName };
					break;
				case OverlayNotificationKind.NumLockOn:
					content = new() { Font = GlyphFont.FluentSystemIcons, Glyph = "\uEB64", Description = request.DeviceName };
					break;
				case OverlayNotificationKind.ScrollLockOff:
					content = new() { Font = GlyphFont.FluentSystemIcons, Glyph = "\uE0BE", Description = request.DeviceName };
					break;
				case OverlayNotificationKind.ScrollLockOn:
					content = new() { Font = GlyphFont.FluentSystemIcons, Glyph = "\uE0BC", Description = request.DeviceName };
					break;
				case OverlayNotificationKind.FnLockOff:
					content = new() { Glyph = "\uE785", Description = request.DeviceName };
					break;
				case OverlayNotificationKind.FnLockOn:
					content = new() { Glyph = "\uE72E", Description = request.DeviceName };
					break;
				case OverlayNotificationKind.MonitorBrightnessDown:
					content = new() { Glyph = "\uEC8A", Description = request.DeviceName, CurrentLevel = (int)request.Level, LevelCount = (int)request.MaxLevel };
					break;
				case OverlayNotificationKind.MonitorBrightnessUp:
					content = new() { Glyph = "\uE706", Description = request.DeviceName, CurrentLevel = (int)request.Level, LevelCount = (int)request.MaxLevel };
					break;
				case OverlayNotificationKind.KeyboardBacklightDown:
					content = new() { Glyph = "\uED3A", Description = request.DeviceName, CurrentLevel = (int)request.Level, LevelCount = (int)request.MaxLevel };
					break;
				case OverlayNotificationKind.KeyboardBacklightUp:
					content = new() { Glyph = "\uED39", Description = request.DeviceName, CurrentLevel = (int)request.Level, LevelCount = (int)request.MaxLevel };
					break;
				case OverlayNotificationKind.BatteryLow:
					content = new() { Glyph = GetBatteryDischargingGlyph(request.MaxLevel == 10 ? request.Level : 1), Description = request.DeviceName };
					break;
				case OverlayNotificationKind.BatteryFullyCharged:
					content = new() { Glyph = BatteryChargingGlyphs[10], Description = request.DeviceName };
					break;
				case OverlayNotificationKind.BatteryExternalPowerDisconnected:
					// TODO: Better glyph when charge status is unknown.
					content = new() { Glyph = request.MaxLevel == 10 ? GetBatteryDischargingGlyph(request.Level) : "\U0001F6C7\uFE0F", Description = request.DeviceName };
					break;
				case OverlayNotificationKind.BatteryExternalPowerConnected:
					content = new() { Glyph = request.MaxLevel == 10 ? GetBatteryChargingGlyph(request.Level) : "\uE945", Description = request.DeviceName };
					break;
				case OverlayNotificationKind.MouseDpiDown:
					content = new() { Glyph = "\uF08E", Description = request.DeviceName, CurrentLevel = (int)request.Level, LevelCount = (int)request.MaxLevel, Value = request.Value > 0 ? request.Value : null };
					break;
				case OverlayNotificationKind.MouseDpiUp:
					content = new() { Glyph = "\uF090", Description = request.DeviceName, CurrentLevel = (int)request.Level, LevelCount = (int)request.MaxLevel, Value = request.Value > 0 ? request.Value : null };
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
		catch (ObjectDisposedException)
		{
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception)
		{
			// TODO: See what exceptions can be thrown when the service is disconnected or the channel is shutdown.
		}
		_overlayChannel.Writer.TryComplete();
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
