using System;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed class NullabilityToBooleanConverter : IValueConverter
{
	private static readonly object True = true;
	private static readonly object False = false;

	public object? Convert(object value, Type targetType, object parameter, string language) => value is not null ? True : False;

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
