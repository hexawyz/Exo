using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
		new PropertyMetadata(null)
	);

	public LightingZoneControl()
	{
		InitializeComponent();
	}
}
