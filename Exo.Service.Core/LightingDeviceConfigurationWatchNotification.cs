namespace Exo.Service;

public readonly struct LightingDeviceConfigurationWatchNotification
{
	/// <summary>Gets the ID of the device.</summary>
	public required Guid DeviceId { get; init; }

	/// <summary>Gets a value indicating if unified lighting is enabled on the device.</summary>
	public required bool IsUnifiedLightingEnabled { get; init; }

	/// <summary>Gets the default brightness level of the device.</summary>
	/// <remarks>
	/// The brightness is expressed in device-specific units.
	/// It corresponds to the default brightness the device will apply to all effects excepts effect overriding the brightness if the device supports it.
	/// </remarks>
	public required byte? BrightnessLevel { get; init; }
}
