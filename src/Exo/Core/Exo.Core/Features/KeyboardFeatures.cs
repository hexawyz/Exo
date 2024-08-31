namespace Exo.Features.Keyboards;

public interface IKeyboardLockKeysFeature : IKeyboardDeviceFeature
{
	/// <summary>This event is raised when the locked keys have changed.</summary>
	event Action<Driver, LockKeys> LockedKeysChanged;

	/// <summary>Gets the current locked keys.</summary>
	LockKeys LockedKeys { get; }
}

public interface IKeyboardBacklightFeature : IKeyboardDeviceFeature
{
	/// <summary>This event is raised when the backlight level of the device has changed.</summary>
	event Action<Driver, BacklightState> BacklightStateChanged;

	/// <summary>Gets the current backlight level.</summary>
	BacklightState BacklightState { get; }
}
