using Exo.Settings.Ui.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed class NotificationSeverityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language)
		=> (value as NotificationSeverity?) switch
		{
			NotificationSeverity.Informational => InfoBarSeverity.Informational,
			NotificationSeverity.Success => InfoBarSeverity.Success,
			NotificationSeverity.Warning => InfoBarSeverity.Warning,
			NotificationSeverity.Error => InfoBarSeverity.Error,
			_ => InfoBarSeverity.Informational,
		};

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
