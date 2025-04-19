using CommunityToolkit.WinUI.Controls;
using Exo.Monitors;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed class MonitorShapeToCropShapeConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language)
		=> value is MonitorShape.Circle ? CropShape.Circular : CropShape.Rectangular;

	public object ConvertBack(object value, Type targetType, object parameter, string language)
		=> throw new NotSupportedException();
}
