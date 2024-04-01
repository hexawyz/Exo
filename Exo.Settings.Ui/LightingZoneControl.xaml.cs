using CommunityToolkit.WinUI.UI;
using CommunityToolkit.WinUI.UI.Controls;
using Exo.Settings.Ui.Services;
using Exo.Settings.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Exo.Settings.Ui;

internal sealed partial class LightingZoneControl : UserControl
{
	public LightingZoneViewModel? LightingZone
	{
		get { return (LightingZoneViewModel)GetValue(LightingZoneProperty); }
		set { SetValue(LightingZoneProperty, value); }
	}

	public static readonly DependencyProperty LightingZoneProperty = DependencyProperty.Register
	(
		nameof(LightingZone),
		typeof(LightingZoneViewModel),
		typeof(LightingZoneControl),
		new PropertyMetadata(null, (d, e) => ((LightingZoneControl)d).OnLightingZonePropertyChanged((LightingZoneViewModel)e.NewValue))
	);

	public LightingZoneControl()
	{
		InitializeComponent();
	}

	private void OnLightingZonePropertyChanged(LightingZoneViewModel value) => ((FrameworkElement)Content).DataContext = value;
}
