using Exo.Memory;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Exo.Settings.Ui;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ImagesPage : Page
    {
        public ImagesPage()
        {
            InitializeComponent();
	}

	private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

	private async void OnOpenButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
	{
		var fileOpenPicker = new FileOpenPicker();
		fileOpenPicker.FileTypeFilter.Add(".bmp");
		fileOpenPicker.FileTypeFilter.Add(".gif");
		fileOpenPicker.FileTypeFilter.Add(".png");
		fileOpenPicker.FileTypeFilter.Add(".jpg");

		InitializeWithWindow.Initialize(fileOpenPicker, WindowNative.GetWindowHandle(App.Current.MainWindow));
		var file = await fileOpenPicker.PickSingleFileAsync();
		if (file is null) return;
		SharedMemory? data;
		using (var stream = await file.OpenStreamForReadAsync())
		{
			long length = stream.Length;
			if (length <= 0)
			{
				data = null;
			}
			else
			{
				data = SharedMemory.Create("Exo_Image_", (ulong)length);
				using (var viewStream = data.CreateWriteStream())
				{
					await stream.CopyToAsync(viewStream);
				}
			}
		}

		if (data is not null)
		{
			ViewModel.Images.SetImage(Path.GetFileNameWithoutExtension(file.Path), data);
		}
		else
		{
			ViewModel.Images.ClearImage();
		}
    }
}
