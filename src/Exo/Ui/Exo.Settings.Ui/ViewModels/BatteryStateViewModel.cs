using Exo.Features;
using Exo.Service;
using WinRT;

namespace Exo.Settings.Ui.ViewModels;

[GeneratedBindableCustomProperty]
internal sealed partial class BatteryStateViewModel
{
	private readonly BatteryChangeNotification _notification;

	public BatteryStateViewModel(BatteryChangeNotification notification) => _notification = notification;

	public float? Level => _notification.Level;
	public BatteryStatus BatteryStatus => _notification.BatteryStatus;
	public ExternalPowerStatus ExternalPowerStatus => _notification.ExternalPowerStatus;
}
