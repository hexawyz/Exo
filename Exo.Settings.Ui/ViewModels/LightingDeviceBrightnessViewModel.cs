using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingDeviceBrightnessViewModel : BindableObject
{
	private readonly LightingBrightnessCapabilities _capabilities;
	private ushort _initialBrightness;
	private ushort _currentBrightness;

	public LightingDeviceBrightnessViewModel(LightingBrightnessCapabilities capabilities) => _capabilities = capabilities;

	public double MinimumLevelPercent => _capabilities.MinimumBrightness / (double)_capabilities.MaximumBrightness;

	public ushort MinimumLevel => _capabilities.MinimumBrightness;
	public ushort MaximumLevel => _capabilities.MaximumBrightness;

	public bool IsChanged => _currentBrightness != _initialBrightness;

	public ushort Level
	{
		get => _currentBrightness;
		set
		{
			bool wasChanged = IsChanged;

			if (SetValue(ref _currentBrightness, value))
			{
				if (IsChanged != wasChanged)
				{
					NotifyPropertyChanged(ChangedProperty.IsChanged);
				}
			}
		}
	}

	public void SetInitialBrightness(ushort value)
	{
		if (_initialBrightness != value)
		{
			bool wasChanged = IsChanged;

			_initialBrightness = value;

			if (IsChanged != wasChanged)
			{
				NotifyPropertyChanged(ChangedProperty.IsChanged);
			}
		}
	}

	public void Reset() => Level = _initialBrightness;
}
