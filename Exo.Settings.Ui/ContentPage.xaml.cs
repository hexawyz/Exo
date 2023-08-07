using System;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Exo.Settings.Ui;

public sealed partial class ContentPage : Page
{
	public ContentPage()
	{
		InitializeComponent();
	}

	private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

	protected override void OnNavigatedTo(NavigationEventArgs e)
	{
		Type? type = null;
		object? dataContext = null;
		string? title = null;

		switch (e.Parameter)
		{
		case "Devices":
			type = typeof(DevicesPage);
			dataContext = ViewModel.Devices;
			title = "Devices";
			break;
		case "Lighting":
			type = typeof(LightingPage);
			dataContext = ViewModel.Lighting;
			title = "Lighting";
			break;
		}

		if (type is null) return;

		if (ContentFrame.CurrentSourcePageType != type)
		{
			ContentFrame.Navigate(type);
			((Page)ContentFrame.Content).DataContext = dataContext;
			ViewModel.Title = title!;
		}

		base.OnNavigatedTo(e);
	}
}
