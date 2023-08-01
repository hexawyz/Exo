using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui;

internal sealed class NullabilityToVisibilityConverter : IValueConverter
{
	private static readonly object Visible = Visibility.Visible;
	private static readonly object Collapsed = Visibility.Collapsed;

	public object? Convert(object value, Type targetType, object parameter, string language) => value is not null ? Visible : Collapsed;

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
