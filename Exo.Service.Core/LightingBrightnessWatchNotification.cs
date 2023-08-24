namespace Exo.Service;

public readonly struct LightingBrightnessWatchNotification
{
	public LightingBrightnessWatchNotification(Guid deviceId, byte brightnessLevel)
	{
		DeviceId = deviceId;
		BrightnessLevel = brightnessLevel;
	}

	/// <summary>Gets the ID of the device.</summary>
	public Guid DeviceId { get; }

	/// <summary>Gets the default brightness level of the device.</summary>
	/// <remarks>
	/// The brightness is expressed in device-specific units.
	/// It corresponds to the default brightness the device will apply to all effects excepts effect overriding the brightness if the device supports it.
	/// </remarks>
	public byte BrightnessLevel { get; }
}
