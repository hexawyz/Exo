using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class NumberWithUnitConverter : DependencyObject, IValueConverter
{
	public string? Unit
	{
		get => (string)GetValue(UnitProperty);
		set => SetValue(UnitProperty, value);
	}

	public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(NumberWithUnitConverter), new PropertyMetadata(null));

	public string? NumberFormat
	{
		get => (string)GetValue(NumberFormatProperty);
		set => SetValue(NumberFormatProperty, value);
	}

	public static readonly DependencyProperty NumberFormatProperty = DependencyProperty.Register(nameof(NumberFormat), typeof(string), typeof(NumberWithUnitConverter), new PropertyMetadata(null));

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is null) return null;

		if (NumberFormat is string format && value is IFormattable formattable)
			value = formattable.ToString(format, null);

		return Unit is string unit ? $"{value}\xA0{unit}" : value.ToString();
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
