using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Exo.Settings.Ui;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
internal sealed partial class MainWindow : Window
{
	public MainWindow()
	{
		ViewModel = new SettingsViewModel();
		InitializeComponent();
	}

	public SettingsViewModel ViewModel { get; }

	private void NavigationItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
	{
		Type? type = null;
		object? dataContext = null;
		switch (args.InvokedItemContainer.Tag)
		{
		case "Devices":
			type = typeof(DevicesPage);
			dataContext = ViewModel.Devices;
			break;
		}

		if (type is null) return;

		if (contentFrame.CurrentSourcePageType != type)
		{
			contentFrame.Navigate(type, args.RecommendedNavigationTransitionInfo);
			((Page)contentFrame.Content).DataContext = dataContext;
		}
    }
}
