using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

// This is a hack to please the x:Bind Bindings, but it should likely be removed.
// In the case of lighting effect data templates, the best outcome is probably to end up specializing for each data type.
// It will be a bit costlier but it should be more efficient also.
internal sealed partial class NullableConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language) => value ?? DependencyProperty.UnsetValue;

	public object? ConvertBack(object value, Type targetType, object parameter, string language) => value;
}
