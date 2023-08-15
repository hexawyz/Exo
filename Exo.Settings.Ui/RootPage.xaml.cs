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
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ViewManagement;

namespace Exo.Settings.Ui;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
internal sealed partial class RootPage : Page
{
	public string AppTitleText => "Exo";

	public SettingsViewModel ViewModel { get; }

	public RootPage(Window window)
	{
		ViewModel = new SettingsViewModel();
		InitializeComponent();

		Loaded += delegate (object sender, RoutedEventArgs e)
		{
			window.Title = AppTitleText;
			window.ExtendsContentIntoTitleBar = true;
			window.SetTitleBar(AppTitleBar);
			window.Activated += OnWindowActivated;
		};
	}

	private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
	{
		if (args.WindowActivationState == WindowActivationState.Deactivated)
		{
			VisualStateManager.GoToState(this, "Deactivated", true);
		}
		else
		{
			VisualStateManager.GoToState(this, "Activated", true);
		}
	}

	private void OnPaneDisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
	{
		if (sender.PaneDisplayMode == NavigationViewPaneDisplayMode.Top)
		{
			VisualStateManager.GoToState(this, "Top", true);
		}
		else
		{
			if (args.DisplayMode == NavigationViewDisplayMode.Minimal)
			{
				VisualStateManager.GoToState(this, "Compact", true);
			}
			else
			{
				VisualStateManager.GoToState(this, "Default", true);
			}
		}
	}

	private void OnNavigationItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
	{
		Type? type = null;
		switch (args.InvokedItemContainer.Tag)
		{
		case "Devices":
			type = typeof(DevicesPage);
			break;
		case "Lighting":
			type = typeof(LightingPage);
			break;
		}

		if (type is null) return;

		if (ContentFrame.CurrentSourcePageType != type)
		{
			ContentFrame.Navigate(type);
		}
	}

	private void OnNavigationBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
	{
		ContentFrame.GoBack();
	}
}
