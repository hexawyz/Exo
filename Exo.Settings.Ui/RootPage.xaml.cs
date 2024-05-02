using Exo.Settings.Ui.Services;
using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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

	public SettingsViewModel ViewModel { get; }

	public RootPage(Window window)
	{
		_window = window;

		ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();

		InitializeComponent();

		Loaded += delegate (object sender, RoutedEventArgs e)
		{
			window.Title = AppTitleText;
			window.ExtendsContentIntoTitleBar = true;
			window.SetTitleBar(AppTitleBar);
			window.Activated += OnWindowActivated;
			AppTitleBar.Loaded += OnAppTitleBarLoaded;
			AppTitleBar.SizeChanged += OnAppTitleBarSizeChanged; ;
			App.Current.Services.GetRequiredService<IEditionService>().ShowToolbar = false;
		};
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

	private void SetRegionsForCustomTitleBar()
	{
		// See: https://learn.microsoft.com/en-us/windows/apps/develop/title-bar#interactive-content
		double scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale;

		var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(_window.AppWindow.Id);
		nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, new[] { GetRect(StatusIconArea, scaleAdjustment) });
	}

	private static RectInt32 GetRect(FrameworkElement element, double scale)
		=> GetRect(element.TransformToVisual(null).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight)), scale);

	private static RectInt32 GetRect(Rect bounds, double scale)
		=> new((int)Math.Round(bounds.X * scale), (int)Math.Round(bounds.Y * scale), (int)Math.Round(bounds.Width * scale), (int)Math.Round(bounds.Height * scale));
}
