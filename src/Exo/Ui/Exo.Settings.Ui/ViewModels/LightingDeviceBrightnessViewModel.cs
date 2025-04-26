using Exo.Service;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingDeviceBrightnessViewModel : ChangeableBindableObject
{
	private readonly BrightnessCapabilities _capabilities;
	private byte _initialBrightness;
	private byte _currentBrightness;

	public LightingDeviceBrightnessViewModel(BrightnessCapabilities capabilities) => _capabilities = capabilities;

	public double MinimumLevelPercent => _capabilities.MinimumValue / (double)_capabilities.MaximumValue;

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
