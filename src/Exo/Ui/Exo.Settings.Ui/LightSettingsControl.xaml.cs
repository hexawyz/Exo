using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace Exo.Settings.Ui;

public sealed partial class LightSettingsControl : UserControl
{
	public LightSettingsControl()
	{
		InitializeComponent();
	}

	private void OnSliderPointerReleased(object sender, PointerRoutedEventArgs e)
		=> UpdateBinding((Slider)sender);

	private void OnSliderKeyUp(object sender, KeyRoutedEventArgs e)
		=> UpdateBinding((Slider)sender);

	private void UpdateBinding(Slider slider)
		=> slider.GetBindingExpression(RangeBase.ValueProperty).UpdateSource();

	private void OnSliderLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
	{
		var slider = (Slider)sender;
		slider.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnSliderPointerReleased), true);
		slider.AddHandler(KeyUpEvent, new KeyEventHandler(OnSliderKeyUp), true);
	}
}
