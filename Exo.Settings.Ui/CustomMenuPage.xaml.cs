using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Exo.Settings.Ui;

public sealed partial class CustomMenuPage : Page
{
	public CustomMenuPage()
	{
		InitializeComponent();
	}

	private SettingsViewModel SettingsViewModel => (SettingsViewModel)DataContext;
	private CustomMenuViewModel CustomMenuViewModel => SettingsViewModel.CustomMenu;

	protected override void OnNavigatedTo(NavigationEventArgs e)
	{
		SettingsViewModel.Icon = "\uEDE3";
		SettingsViewModel.Title = "Custom Menu";
	}

	private void OnBreadcrumbBarItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
		=> CustomMenuViewModel.NavigateToSubMenuCommand.Execute(args.Item);
}
