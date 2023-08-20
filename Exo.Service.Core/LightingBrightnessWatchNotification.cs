using System;

namespace Exo.Service;

public readonly struct LightingBrightnessWatchNotification
{
	public LightingBrightnessWatchNotification(Guid deviceId, byte brightnessLevel)
	{
		DeviceId = deviceId;
		BrightnessLevel = brightnessLevel;
	}

	/// <summary>Gets the ID of the device on which the brightness was applied.</summary>
	public Guid DeviceId { get; }

	/// <summary>Gets the brightness level that was applied.</summary>
	public byte BrightnessLevel { get; }

}
