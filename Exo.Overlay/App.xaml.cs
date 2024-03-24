using System.Diagnostics;
using System.IO;
using Application = System.Windows.Application;

namespace Exo.Overlay;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	// NB: Use legacy icon IDs for now.
	//private static readonly Guid IconGuid = new(0x7DBD82ED, 0x40E8, 0x430C, 0x9B, 0x92, 0x55, 0x57, 0x95, 0x65, 0x66, 0x03);

	private static readonly string? SettingsUiExecutablePath = LocateSettingsUi();

	private NotifyIcon? _icon;

	protected override async void OnStartup(System.Windows.StartupEventArgs e)
	{
		// Setup the notification icon using interop code in order to get the native UI.
		// Sadly, the current state of notification icons is a total mess, and each app uses its own shitty implementation so there is no coherence at all.
		// Using native calls at least gives the basic look&feel, but then we're still out of style… We can use this undocumented uxtheme API (always… why!) to allow dark mode at least.
		NativeMethods.SetPreferredAppMode(1);
		var window = await NotificationWindow.GetOrCreateAsync().ConfigureAwait(false);
		await window.SwitchTo();
		_icon = window.CreateNotifyIcon(0, 32512, "Exo");
		var settingsMenuItem = new TextMenuItem("&Settings…");
		settingsMenuItem.Click += OnSettingsMenuItemClick;
		settingsMenuItem.IsEnabled = SettingsUiExecutablePath is not null;
		var exitMenuItem = new TextMenuItem("E&xit");
		exitMenuItem.Click += OnExitMenuItemClick;
		_icon.ContextMenu.Add(settingsMenuItem);
		_icon.ContextMenu.Add(new SeparatorMenuItem());
		_icon.ContextMenu.Add(exitMenuItem);
	}

	private void OnSettingsMenuItemClick(object? sender, EventArgs e)
	{
		if (SettingsUiExecutablePath is not null)
		{
			Process.Start(SettingsUiExecutablePath);
		}
	}

	private async void OnExitMenuItemClick(object? sender, EventArgs e)
	{
		if (_icon != null)
		{
			_icon!.Dispose();
			_icon = null;
			await Dispatcher.InvokeAsync(() => Shutdown(0));
		}
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
}

