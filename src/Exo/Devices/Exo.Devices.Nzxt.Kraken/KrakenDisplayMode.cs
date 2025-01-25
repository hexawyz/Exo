namespace Exo.Devices.Nzxt.Kraken;

/// <summary>Represents a display mode supported by the device.</summary>
/// <remarks><see cref=" KrakenPresetVisual"/> is a subset of this enum containing only the preset visual modes.</remarks>
internal enum KrakenDisplayMode : byte
{
	Off = 0,
	Animation = 1,
	LiquidTemperature = 2,
	StoredImage = 4,
	QuickImage = 5,
}
