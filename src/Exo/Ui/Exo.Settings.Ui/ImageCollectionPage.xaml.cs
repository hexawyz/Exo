using System.ComponentModel;
using Exo.Settings.Ui.ViewModels;
using Exo.Ui;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ImageCollectionPage : Page
{
	public ImageCollectionPage()
	{
		InitializeComponent();
	}

	private ImagesViewModel? _imagesViewModel;
	private int _shouldIgnoreSelectedItemChanged;

	private void OnItemsViewSelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
	{
		if (_imagesViewModel is not null && _shouldIgnoreSelectedItemChanged == 0)
		{
			_shouldIgnoreSelectedItemChanged++;
			try
			{
				_imagesViewModel.SelectedImage = sender.SelectedItem as ImageViewModel;
			}
			finally
			{
				_shouldIgnoreSelectedItemChanged--;
			}
		}
	}

	private void OnItemsViewDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
	{
		var newValue = args.NewValue as ImagesViewModel;
		if (!ReferenceEquals(newValue, _imagesViewModel))
		{
			_shouldIgnoreSelectedItemChanged++;
			try
			{
				if (_imagesViewModel is not null) _imagesViewModel.PropertyChanged -= OnImagesViewModelPropertyChanged;
				_imagesViewModel = newValue;
				if (_imagesViewModel is not null)
				{
					if (_imagesViewModel!.SelectedImage is { } selectedImage && _imagesViewModel.Images.IndexOf(selectedImage) is int index and >= 0)
					{
						ImageItemsView.Select(index);
					}
					_imagesViewModel.PropertyChanged += OnImagesViewModelPropertyChanged;
				}
			}
			finally
			{
				_shouldIgnoreSelectedItemChanged--;
			}
		}
	}

	private void OnImagesViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (BindableObject.Equals(e, ChangedProperty.SelectedImage))
		{
			if (_imagesViewModel!.SelectedImage is { } selectedImage)
			{
				if (_imagesViewModel.Images.IndexOf(selectedImage) is int index and >= 0)
				{
					ImageItemsView.Select(index);
				}
			}
		}
	}
}
