using Exo.Settings.Ui.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class ConnectionStatusToObjectConverter : DependencyObject, IValueConverter
{
	public object DisconnectedValue
	{
		get => GetValue(DisconnectedValueProperty);
		set => SetValue(DisconnectedValueProperty, value);
	}

	public static readonly DependencyProperty DisconnectedValueProperty =
		DependencyProperty.Register(nameof(DisconnectedValue), typeof(object), typeof(ConnectionStatusToObjectConverter), new PropertyMetadata(null));

	public object ConnectedValue
	{
		get => GetValue(ConnectedValueProperty);
		set => SetValue(ConnectedValueProperty, value);
	}

	public static readonly DependencyProperty ConnectedValueProperty =
		DependencyProperty.Register(nameof(ConnectedValue), typeof(object), typeof(ConnectionStatusToObjectConverter), new PropertyMetadata(null));

	public object VersionMismatchValue
	{
		get => GetValue(VersionMismatchValueProperty);
		set => SetValue(VersionMismatchValueProperty, value);
	}

	public static readonly DependencyProperty VersionMismatchValueProperty =
		DependencyProperty.Register(nameof(VersionMismatchValue), typeof(object), typeof(ConnectionStatusToObjectConverter), new PropertyMetadata(null));

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is ConnectionStatus status)
		{
			switch (status)
			{
			case ConnectionStatus.Disconnected: return DisconnectedValue;
			case ConnectionStatus.Connected: return ConnectedValue;
			case ConnectionStatus.VersionMismatch: return VersionMismatchValue;
			}
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
