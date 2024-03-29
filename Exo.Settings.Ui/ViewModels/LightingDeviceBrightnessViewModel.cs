using Exo.Contracts.Ui.Settings;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class LightingDeviceBrightnessViewModel : ChangeableBindableObject
{
	private readonly LightingBrightnessCapabilities _capabilities;
	private byte _initialBrightness;
	private byte _currentBrightness;

	public LightingDeviceBrightnessViewModel(LightingBrightnessCapabilities capabilities) => _capabilities = capabilities;

	public double MinimumLevelPercent => _capabilities.MinimumBrightness / (double)_capabilities.MaximumBrightness;

	public byte MinimumLevel => _capabilities.MinimumBrightness;
	public byte MaximumLevel => _capabilities.MaximumBrightness;

	public override bool IsChanged => _currentBrightness != _initialBrightness;

	public byte Level
	{
		get => _currentBrightness;
		set
		{
			bool wasChanged = IsChanged;

			if (SetValue(ref _currentBrightness, value))
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

			if (wasCurrentValueInitial)
			{
				_currentBrightness = value;
			}

			OnChangeStateChange(wasChanged);
		}
	}

	public void Reset() => Level = _initialBrightness;
}
