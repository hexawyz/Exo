namespace Exo.Service;

[Flags]
public enum EmbeddedMonitorCapabilities : uint
{
	None = 0b00000000,
	StaticImages = 0b00000001,
	AnimatedImages = 0b00000010,
	PartialUpdates = 0b00000100,
	BuiltInGraphics = 0b00001000,
	ScreensaverImage = 0b00010000,
}
