using System.ComponentModel;
using Exo.Settings.Ui.ViewModels;
using Exo.Ui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
internal sealed partial class ImageCollectionPage : Page
{
	public SettingsViewModel SettingsViewModel { get; }
	public ImagesViewModel Images { get; }

	public ImageCollectionPage()
	{
		SettingsViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
		Images = SettingsViewModel.Images;
		InitializeComponent();

		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (Images.SelectedImage is { } selectedImage && Images.Images.IndexOf(selectedImage) is int index and >= 0)
		{
			ImageItemsView.Select(index);
		}
		Images.PropertyChanged += OnImagesViewModelPropertyChanged;
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
		=> Images.PropertyChanged -= OnImagesViewModelPropertyChanged;

	private int _shouldIgnoreSelectedItemChanged;

	private void OnItemsViewSelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
	{
		if (_shouldIgnoreSelectedItemChanged == 0)
		{
			_shouldIgnoreSelectedItemChanged++;
			try
			{
				Images.SelectedImage = sender.SelectedItem as ImageViewModel;
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
			if (Images.SelectedImage is { } selectedImage)
			{
				if (Images.Images.IndexOf(selectedImage) is int index and >= 0)
				{
					ImageItemsView.Select(index);
				}
			}
		}
	}
}
