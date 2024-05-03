using Microsoft.UI.Xaml.Data;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Exo.Settings.Ui;

internal sealed class StringResourceConverter : IValueConverter
{
	private static readonly ResourceManager ResourceManager = new ResourceManager();

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		try
		{
			return value is string name ?
				ResourceManager.MainResourceMap.GetValue(parameter is string directory ? $"{directory}/{name}" : name).ValueAsString :
				null;
		}
		catch
		{
			return null;
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
