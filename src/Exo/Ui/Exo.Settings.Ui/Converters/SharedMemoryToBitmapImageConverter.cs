using Exo.Memory;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Exo.Settings.Ui.Converters;

internal sealed class SharedMemoryToBitmapImageConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not SharedMemory data) return null;

		var bitmapImage = new BitmapImage();
		LoadImage(bitmapImage, data);
		return bitmapImage;
	}

	private async void LoadImage(BitmapImage bitmapImage, SharedMemory sharedMemory)
	{
		try
		{
			using (var sharedMemoryStream = sharedMemory.CreateReadStream())
			using (var randomAccessStream = sharedMemoryStream.AsRandomAccessStream())
			{
				await bitmapImage.SetSourceAsync(randomAccessStream);
			}
		}
		catch
		{
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
