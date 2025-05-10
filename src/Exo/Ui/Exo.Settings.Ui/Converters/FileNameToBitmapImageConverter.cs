using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Exo.Settings.Ui.Converters;

internal sealed partial class FileNameToBitmapImageConverter : IValueConverter
{
	private readonly Dictionary<string, WeakReference<BitmapImage>> _bitmapCache = new(StringComparer.OrdinalIgnoreCase);

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not string fileName) return null;

		// In this case, we take the bet that the filename and the file will "always" be valid. As such, we pre-allocate a weak reference before knowing if the file will be read successfully.
		BitmapImage? bitmapImage;
		if (_bitmapCache.TryGetValue(fileName, out var wr))
		{
			if (wr.TryGetTarget(out bitmapImage)) return bitmapImage;
			bitmapImage = new();
			wr.SetTarget(bitmapImage);
		}
		else
		{
			bitmapImage = new();
			_bitmapCache.Add(fileName, new(bitmapImage, false));
		}

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
