using System.ComponentModel;
using Exo.Settings.Ui.ViewModels;
using Exo.Ui;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

internal sealed partial class MousePerformanceSettingsControl : UserControl
{
	public MouseDeviceFeaturesViewModel? MouseFeatures
	{
		get => (MouseDeviceFeaturesViewModel)GetValue(MouseFeaturesProperty);
		set => SetValue(MouseFeaturesProperty, value);
	}

	public static readonly DependencyProperty MouseFeaturesProperty = DependencyProperty.Register
	(
		nameof(MouseFeatures),
		typeof(MouseDeviceFeaturesViewModel),
		typeof(MousePerformanceSettingsControl),
		new PropertyMetadata(null, (s, e) => ((MousePerformanceSettingsControl)s).OnMouseDeviceFeaturesChanged(e.OldValue as MouseDeviceFeaturesViewModel, e.NewValue as MouseDeviceFeaturesViewModel))
	);

	// ItemsView is a garbage fire.
	private int _shouldIgnoreSelectionChangeBecauseItemsViewSucks;

	public MousePerformanceSettingsControl()
	{
		InitializeComponent();
	}

	private void OnMouseDeviceFeaturesChanged(MouseDeviceFeaturesViewModel? oldValue, MouseDeviceFeaturesViewModel? newValue)
	{
		if (!ReferenceEquals(oldValue, newValue))
		{
			_shouldIgnoreSelectionChangeBecauseItemsViewSucks++;
			try
			{
				if (oldValue is not null)
				{
					oldValue.PropertyChanged -= OnMouseFeaturesPropertyChanged;
					DpiPresetsItemView.ItemsSource = null;
				}
				if (newValue is not null)
				{
					DpiPresetsItemView.ItemsSource = newValue.DpiPresets;
					ProcessSelectedDpiPresetIndexPropertyChange(newValue);
					newValue.PropertyChanged += OnMouseFeaturesPropertyChanged;
				}
			}
			finally
			{
				_shouldIgnoreSelectionChangeBecauseItemsViewSucks--;
			}
		}
	}

	private void OnMouseFeaturesPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// NB: We should not listen to SelectedDpiPresetIndex changes, as the value can stay the same with the active selection needing to change.
		if (sender is MouseDeviceFeaturesViewModel vm && BindableObject.Equals(e, ChangedProperty.SelectedDpiPreset))
		{
			ProcessSelectedDpiPresetIndexPropertyChange(vm);
		}
	}

	private void ProcessSelectedDpiPresetIndexPropertyChange(MouseDeviceFeaturesViewModel vm)
	{
		int newIndex = vm.SelectedDpiPresetIndex;
		var selectedItem = vm.SelectedDpiPreset;
		if (DpiPresetsItemView.SelectedItem != selectedItem)
		{
			_shouldIgnoreSelectionChangeBecauseItemsViewSucks++;
			try
			{
				if (newIndex < 0 || newIndex >= vm.DpiPresets.Count)
				{
					DpiPresetsItemView.DeselectAll();
				}
				else
				{
					DpiPresetsItemView.Select(newIndex);
				}
			}
			finally
			{
				_shouldIgnoreSelectionChangeBecauseItemsViewSucks--;
			}
		}
	}

	private void OnDpiPresetsItemsViewSelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
	{
		if (sender.ItemsSource is null || _shouldIgnoreSelectionChangeBecauseItemsViewSucks > 0 || MouseFeatures is not { } mouseFeatures) return;

		int newIndex = -1;
		if (DpiPresetsItemView.SelectedItem is MouseDpiPresetViewModel selectedPreset)
		{
			newIndex = mouseFeatures.DpiPresets.IndexOf(selectedPreset);
		}

		mouseFeatures.SelectedDpiPresetIndex = newIndex;
	}
}
