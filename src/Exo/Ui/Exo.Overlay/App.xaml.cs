using System.IO;
using System.Threading.Channels;
using Exo.Contracts.Ui;
using Exo.Contracts.Ui.Overlay;
using Exo.Rpc;
using Application = System.Windows.Application;

namespace Exo.Overlay;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
internal partial class App : Application
{
	// NB: Use legacy icon IDs for now.
	//private static readonly Guid IconGuid = new(0x7DBD82ED, 0x40E8, 0x430C, 0x9B, 0x92, 0x55, 0x57, 0x95, 0x65, 0x66, 0x03);

	public static readonly string? SettingsUiExecutablePath = LocateSettingsUi();

	private readonly ResettableChannel<MenuChangeNotification> _menuChannel;
	private readonly ResettableChannel<MonitorControlProxyRequest> _monitorControlProxyRequestChannel;
	private readonly ExoHelperPipeClient _client;

	private readonly OverlayViewModel _overlayViewModel;
	private NotifyIconService? _notifyIconService;
	private MonitorControlProxy? _monitorControlProxy;
	private readonly CancellationTokenSource _cancellationTokenSource;

	public static new App Current => (App)Application.Current;

	public OverlayViewModel OverlayViewModel => _overlayViewModel!;

	private App()
	{
		_cancellationTokenSource = new();
		var channelOptions = new UnboundedChannelOptions() { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false };
		var overlayRequestChannel = Channel.CreateUnbounded<OverlayRequest>(channelOptions);
		_menuChannel = new(channelOptions);
		_monitorControlProxyRequestChannel = new(channelOptions);
		_client = new("Local\\Exo.Service.Helper", overlayRequestChannel, _menuChannel, _monitorControlProxyRequestChannel);
		_overlayViewModel = new(overlayRequestChannel);
	}

	protected override async void OnStartup(System.Windows.StartupEventArgs e)
	{
		// Use this undocumented uxtheme API (always… why!) to allow system dark mode for classical UI.
		NativeMethods.SetPreferredAppMode(1);

		_notifyIconService = await NotifyIconService.CreateAsync(_menuChannel, _client).ConfigureAwait(false);
		_monitorControlProxy = new MonitorControlProxy(_monitorControlProxyRequestChannel, _client);

		try
		{
			await _client.StartAsync(_cancellationTokenSource.Token);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (ObjectDisposedException)
		{
			return;
		}
		catch
		{
			Shutdown(-1);
			return;
		}
	}

	private static string? LocateSettingsUi()
	{
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
		var applicationDirectory = typeof(App).Assembly.Location;
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
		applicationDirectory = applicationDirectory is not { Length: > 0 } ? AppContext.BaseDirectory : Path.GetDirectoryName(applicationDirectory);

		if (applicationDirectory is not { Length: > 0 }) return null;

		string settingsDirectory = Path.GetFullPath(Path.Combine(applicationDirectory, "../Exo.Settings.Ui"));
		if (Directory.Exists(settingsDirectory))
		{
			string settingsExecutablePath = Path.Combine(settingsDirectory, "Exo.Settings.Ui.exe");
			if (File.Exists(settingsExecutablePath))
			{
				return settingsExecutablePath;
			}
		}
		return null;
	}

	internal async ValueTask RequestShutdown()
	{
		_cancellationTokenSource.Cancel();
		if (_notifyIconService is { } notifyIconService)
		{
			await notifyIconService.DisposeAsync().ConfigureAwait(false);
		}
		if (_monitorControlProxy is { } monitorControlProxy)
		{
			await monitorControlProxy.DisposeAsync().ConfigureAwait(false);
		}
		await _client.DisposeAsync().ConfigureAwait(false);
		await Dispatcher.InvokeAsync(() => Shutdown(0));
	}
}
