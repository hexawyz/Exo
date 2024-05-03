using Exo.Settings.Ui.Services;
using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Graphics;

namespace Exo.Settings.Ui;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
internal sealed partial class RootPage : Page
{
	private readonly Window _window;

	public string AppTitleText => "Exo";

	public SettingsViewModel? ViewModel => (SettingsViewModel)DataContext;

	public RootPage(Window window)
	{
		_window = window;

		InitializeComponent();

		Loaded += delegate (object sender, RoutedEventArgs e)
		{
			var vm = App.Current.Services.GetRequiredService<SettingsViewModel>();

			DataContext = vm;

			vm.PropertyChanged += OnViewModelPropertyChanged;

			window.Title = AppTitleText;
			window.ExtendsContentIntoTitleBar = true;
			window.SetTitleBar(AppTitleBar);
			window.Activated += OnWindowActivated;
			AppTitleBar.Loaded += OnAppTitleBarLoaded;
			AppTitleBar.SizeChanged += OnAppTitleBarSizeChanged; ;
			App.Current.Services.GetRequiredService<IEditionService>().ShowToolbar = false;

			Navigate(vm.CurrentPage);
		};
	}

	private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e == ChangedProperty.CurrentPage || e.PropertyName == ChangedProperty.CurrentPage.PropertyName)
		{
			Navigate(((SettingsViewModel?)sender)?.CurrentPage);
		}
	}

	private void OnAppTitleBarSizeChanged(object sender, SizeChangedEventArgs e) => SetRegionsForCustomTitleBar();

	private void OnAppTitleBarLoaded(object sender, RoutedEventArgs e) => SetRegionsForCustomTitleBar();

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
		if (args.InvokedItemContainer.Tag is not PageViewModel page) return;

		if (ViewModel is { } vm && vm.NavigateCommand.CanExecute(page))
		{
			vm.NavigateCommand.Execute(page);
		}
	}

	private void OnNavigationBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
	{
		if (ViewModel is { } vm && vm.GoBackCommand.CanExecute(null))
		{
			vm.GoBackCommand.Execute(null);
		}
	}

	private void Navigate(PageViewModel? page)
	{
		Type? type = null;

		if (page is not null)
		{
			switch (page.Name)
			{
			case "Home":
				type = typeof(HomePage);
				break;
			case "Devices":
				type = typeof(DevicesPage);
				break;
			case "Device":
				type = typeof(DevicePage);
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
		}
		else
		{
			type = typeof(HomePage);
		}

		if (ContentFrame.CurrentSourcePageType != type)
		{
			ContentFrame.Navigate(type, page?.Parameter);
		}
	}

	private void SetRegionsForCustomTitleBar()
	{
		// See: https://learn.microsoft.com/en-us/windows/apps/develop/title-bar#interactive-content
		double scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale;

		var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(_window.AppWindow.Id);
		nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, [GetRect(StatusIconArea, scaleAdjustment)]);
	}

	private static RectInt32 GetRect(FrameworkElement element, double scale)
		=> GetRect(element.TransformToVisual(null).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight)), scale);

	private static RectInt32 GetRect(Rect bounds, double scale)
		=> new((int)Math.Round(bounds.X * scale), (int)Math.Round(bounds.Y * scale), (int)Math.Round(bounds.Width * scale), (int)Math.Round(bounds.Height * scale));
}
