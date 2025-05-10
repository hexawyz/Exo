using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

public sealed partial class CustomMenuPage : Page
{
	private SettingsViewModel SettingsViewModel { get; }
	private CustomMenuViewModel CustomMenu { get; }

	public CustomMenuPage()
	{
		SettingsViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
		CustomMenu = SettingsViewModel.CustomMenu;
		InitializeComponent();
	}

	private void OnBreadcrumbBarItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
		=> CustomMenu.NavigateToSubMenuCommand.Execute(args.Item);
}
