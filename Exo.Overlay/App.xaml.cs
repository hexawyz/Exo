using Application = System.Windows.Application;

namespace Exo.Overlay;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	private static readonly Guid IconGuid = new(0x7DBD82ED, 0x40E8, 0x430C, 0x9B, 0x92, 0x55, 0x57, 0x95, 0x65, 0x66, 0x03);

	private NotifyIcon? _icon;

	protected override void OnStartup(System.Windows.StartupEventArgs e)
	{
		_icon = new NotifyIcon(IconGuid, 32512, "Exo");
	}
}

