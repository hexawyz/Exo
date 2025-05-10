using System.Collections.ObjectModel;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace Exo.Settings.Ui;

internal sealed partial class LightSettingsControl : UserControl
{
	public ReadOnlyObservableCollection<LightViewModel>? LightCollection
	{
		get => (ReadOnlyObservableCollection<LightViewModel>)GetValue(LightCollectionProperty);
		set => SetValue(LightCollectionProperty, value);
	}

	public static readonly DependencyProperty LightCollectionProperty = DependencyProperty.Register
	(
		nameof(LightCollection),
		typeof(ReadOnlyObservableCollection<LightViewModel>),
		typeof(LightSettingsControl),
		new PropertyMetadata(null)
	);

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

	private void OnSliderLoaded(object sender, RoutedEventArgs e)
	{
		var slider = (Slider)sender;
		slider.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnSliderPointerReleased), true);
		slider.AddHandler(KeyUpEvent, new KeyEventHandler(OnSliderKeyUp), true);
	}
}
