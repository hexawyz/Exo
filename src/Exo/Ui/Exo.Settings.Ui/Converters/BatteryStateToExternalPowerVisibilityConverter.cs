using Exo.Features;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed class BatteryStateToExternalPowerVisibilityConverter : IValueConverter
{
	private static readonly object Visible = Visibility.Visible;
	private static readonly object Collapsed = Visibility.Collapsed;

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not ViewModels.BatteryStateViewModel state) return Collapsed;

		bool isPowered = (state.ExternalPowerStatus & ExternalPowerStatus.IsConnected) != 0;
		bool isChargingOrComplete = state.BatteryStatus is BatteryStatus.Charging or BatteryStatus.ChargingNearlyComplete or BatteryStatus.ChargingComplete;

		return isPowered || isChargingOrComplete ? Visible : Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
