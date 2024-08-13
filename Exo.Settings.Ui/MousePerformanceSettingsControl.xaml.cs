using System.ComponentModel;
using Exo.Settings.Ui.ViewModels;
using Exo.Ui;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exo.Settings.Ui;

public sealed partial class MousePerformanceSettingsControl : UserControl
{
	private MouseDeviceFeaturesViewModel? _viewModel;
	// ItemsView is a garbage fire.
	private int _shouldIgnoreSelectionChangeBecauseItemsViewSucks;

	public MousePerformanceSettingsControl()
	{
		InitializeComponent();
		DataContextChanged += OnDataContextChanged;
	}

	private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
	{
		var oldValue = _viewModel;
		_viewModel = args.NewValue as MouseDeviceFeaturesViewModel;

		if (!ReferenceEquals(oldValue, _viewModel))
		{
			_shouldIgnoreSelectionChangeBecauseItemsViewSucks++;
			try
			{
				if (oldValue is not null)
				{
					oldValue.PropertyChanged -= OnMouseFeaturesPropertyChanged;
					DpiPresetsItemView.ItemsSource = null;
				}
				if (_viewModel is not null)
				{
					DpiPresetsItemView.ItemsSource = _viewModel.DpiPresets;
					ProcessSelectedDpiPresetIndexPropertyChange(_viewModel);
					_viewModel.PropertyChanged += OnMouseFeaturesPropertyChanged;
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
		if (DpiPresetsItemView.CurrentItemIndex != newIndex)
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
		if (sender.ItemsSource is null || _shouldIgnoreSelectionChangeBecauseItemsViewSucks > 0 || sender.DataContext is not MouseDeviceFeaturesViewModel mouseFeatures) return;

		mouseFeatures.SelectedDpiPresetIndex = sender.CurrentItemIndex;
	}
}
