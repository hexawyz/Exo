using System.IO;
using Exo.Ui;
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

	private readonly ServiceConnectionManager _connectionManager = new
	(
		"Local\\Exo.Service.Overlay",
		100,
#if DEBUG
		null
#else
		Exo.Utils.GitCommitHelper.GetCommitId(typeof(App).Assembly)
#endif
	);

	private OverlayViewModel? _overlayViewModel;
	private NotifyIconService? _notifyIconService;

	public static new App Current => (App)Application.Current;

	public OverlayViewModel OverlayViewModel => _overlayViewModel!;

	protected override async void OnStartup(System.Windows.StartupEventArgs e)
	{
		// Use this undocumented uxtheme API (always… why!) to allow system dark mode for classical UI.
		NativeMethods.SetPreferredAppMode(1);

		_overlayViewModel = new(_connectionManager);

		_notifyIconService = await NotifyIconService.CreateAsync(_connectionManager).ConfigureAwait(false);
	}

	private static string? LocateSettingsUi()
	{
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
		var applicationDirectory = typeof(App).Assembly.Location;
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
		applicationDirectory = applicationDirectory is not { Length: > 0 } ? AppContext.BaseDirectory : Path.GetDirectoryName(applicationDirectory);

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
		await Dispatcher.InvokeAsync(() => Shutdown(0));
	}
}

