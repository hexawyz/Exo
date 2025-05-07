using Exo.Service;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingDeviceBrightnessViewModel : ChangeableBindableObject
{
	private BrightnessCapabilities _capabilities;
	private byte _initialBrightness;
	private byte _currentBrightness;

	public LightingDeviceBrightnessViewModel(BrightnessCapabilities capabilities) => _capabilities = capabilities;

	internal void UpdateInformation(BrightnessCapabilities capabilities)
	{
		bool isMinimumChanged = capabilities.MinimumValue != _capabilities.MinimumValue;
		bool isMaximumChanged = capabilities.MaximumValue != _capabilities.MaximumValue;

		if (isMinimumChanged || isMaximumChanged)
		{
			_capabilities = capabilities;
			// TODO: Adjust the current and initial values to force them within the limits. (Being careful about UI side changes)
			//var currentValue = _currentBrightness;
			if (isMinimumChanged) NotifyPropertyChanged(ChangedProperty.MinimumLevel);
			if (isMinimumChanged) NotifyPropertyChanged(ChangedProperty.MaximumLevel);
		}
	}

	// TODO: Remove (doesn't seem to be used)
	//public double MinimumLevelPercent => _capabilities.MinimumValue / (double)_capabilities.MaximumValue;

	public byte MinimumLevel => _capabilities.MinimumValue;
	public byte MaximumLevel => _capabilities.MaximumValue;

	public override bool IsChanged => _currentBrightness != _initialBrightness;

	public byte Level
	{
		get => _currentBrightness;
		set
		{
			bool wasChanged = IsChanged;

			if (SetValue(ref _currentBrightness, value, ChangedProperty.Level))
			{
				OnChangeStateChange(wasChanged);
			}
		}
	}

	public void SetInitialBrightness(byte value)
	{
		if (_initialBrightness != value)
		{
			bool wasChanged = IsChanged;

			bool wasCurrentValueInitial = _initialBrightness == _currentBrightness;

			_initialBrightness = value;

			if (wasCurrentValueInitial && _currentBrightness != value)
			{
				_currentBrightness = value;
				NotifyPropertyChanged(ChangedProperty.Level);
			}

			OnChangeStateChange(wasChanged);
		}
	}

	public void Reset() => Level = _initialBrightness;
}
