using System.Collections.ObjectModel;

namespace Exo.DeviceNotifications.Tester
{
	internal sealed class GlobalConfigurationViewModel : BindableObject
	{
		public ObservableCollection<DeviceViewModel> Devices { get; } = new();
	}
}
