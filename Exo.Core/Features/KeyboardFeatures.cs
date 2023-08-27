namespace Exo.Features.KeyboardFeatures;

public interface IKeyboardLockKeysFeature : IKeyboardDeviceFeature
{
}

public interface IKeyboardBacklightFeature : IKeyboardDeviceFeature
{
	/// <summary>This event is raised when the backlight level of the device has changed.</summary>
	event Action<Driver, BacklightState> BacklightStateChanged;

	/// <summary>Gets the current backlight level.</summary>
	BacklightState BacklightState { get; }
}
