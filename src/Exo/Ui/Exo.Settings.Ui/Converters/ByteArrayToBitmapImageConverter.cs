using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class ByteArrayToBitmapImageConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not byte[] data) return null;

		var bitmapImage = new BitmapImage();
		LoadImage(bitmapImage, data);
		return bitmapImage;
	}

	private async void LoadImage(BitmapImage bitmapImage, byte[] data)
	{
		try
		{
			using (var memoryStream = new MemoryStream(data, 0, data.Length, true, true))
			using (var randomAccessStream = memoryStream.AsRandomAccessStream())
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
