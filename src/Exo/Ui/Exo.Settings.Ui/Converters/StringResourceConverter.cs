using Microsoft.UI.Xaml.Data;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class StringResourceConverter : IValueConverter
{
	private static readonly ResourceManager ResourceManager = new ResourceManager();

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		try
		{
			if (value is Enum) value = value.ToString()!;
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
