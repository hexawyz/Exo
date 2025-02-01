using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Exo.Settings.Ui.Converters;

internal sealed class ImageToWriteableBitmapConverter : IValueConverter
{
	private readonly Dictionary<string, WeakReference<WriteableBitmap>> _bitmapCache = new(StringComparer.OrdinalIgnoreCase);

	public object? Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is not ImageViewModel image) return null;

		// In this case, we take the bet that the filename and the file will "always" be valid. As such, we pre-allocate a weak reference before knowing if the file will be read successfully.
		WriteableBitmap? bitmapImage;
		if (_bitmapCache.TryGetValue(image.FileName, out var wr))
		{
			if (wr.TryGetTarget(out bitmapImage)) return bitmapImage;
			bitmapImage = new(image.Width, image.Height);
			wr.SetTarget(bitmapImage);
		}
		else
		{
			bitmapImage = new(image.Width, image.Height);
			_bitmapCache.Add(image.FileName, new(bitmapImage, false));
		}

		LoadImage(bitmapImage, image.FileName);
		return bitmapImage;
	}

	private async void LoadImage(WriteableBitmap bitmapImage, string fileName)
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
