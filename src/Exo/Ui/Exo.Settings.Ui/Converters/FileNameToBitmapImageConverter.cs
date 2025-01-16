using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Exo.Settings.Ui.Converters;

internal sealed class FileNameToBitmapImageConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not string fileName) return null;

		var bitmapImage = new BitmapImage();
		LoadImage(bitmapImage, fileName);
		return bitmapImage;
	}

	private async void LoadImage(BitmapImage bitmapImage, string fileName)
	{
		try
		{
			using (var randomAccessStream = await FileRandomAccessStream.OpenAsync(fileName, Windows.Storage.FileAccessMode.Read, Windows.Storage.StorageOpenOptions.AllowOnlyReaders, FileOpenDisposition.OpenExisting))
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
