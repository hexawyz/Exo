using Application = System.Windows.Application;

namespace Exo.Overlay;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	// NB: Use legacy icon IDs for now.
	//private static readonly Guid IconGuid = new(0x7DBD82ED, 0x40E8, 0x430C, 0x9B, 0x92, 0x55, 0x57, 0x95, 0x65, 0x66, 0x03);

	private NotifyIcon? _icon;

	protected override async void OnStartup(System.Windows.StartupEventArgs e)
	{
		var window = await NotificationWindow.GetOrCreateAsync().ConfigureAwait(false);
		await window.SwitchTo();
		_icon = window.CreateNotifyIcon(0, 32512, "Exo");
		var settingsMenuItem = new TextMenuItem("&Settings…");
		settingsMenuItem.Click += OnSettingsMenuItemClick;
		var exitMenuItem = new TextMenuItem("E&xit");
		exitMenuItem.Click += OnExitMenuItemClick;
		_icon.ContextMenu.Add(settingsMenuItem);
		_icon.ContextMenu.Add(new SeparatorMenuItem());
		_icon.ContextMenu.Add(exitMenuItem);
	}

	private void OnSettingsMenuItemClick(object? sender, EventArgs e)
	{
	}

	private async void OnExitMenuItemClick(object? sender, EventArgs e) => await Dispatcher.InvokeAsync(() => Shutdown(0));
}

