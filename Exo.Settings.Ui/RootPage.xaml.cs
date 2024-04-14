using Exo.Settings.Ui.Services;
using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
		ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();

		InitializeComponent();

		Loaded += delegate (object sender, RoutedEventArgs e)
		{
			window.Title = AppTitleText;
			window.ExtendsContentIntoTitleBar = true;
			window.SetTitleBar(AppTitleBar);
			window.Activated += OnWindowActivated;
			App.Current.Services.GetRequiredService<IEditionService>().ShowToolbar = false;
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
		case "Sensors":
			type = typeof(SensorsPage);
			break;
		case "CustomMenu":
			type = typeof(CustomMenuPage);
			break;
		case "Programming":
			type = typeof(ProgrammingPage);
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
