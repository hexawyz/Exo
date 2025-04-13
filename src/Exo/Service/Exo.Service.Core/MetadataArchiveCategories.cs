namespace Exo.Service;

[Flags]
public enum MetadataArchiveCategories : byte
{
	None = 0,
	Strings = 1,
	LightingEffects = 2,
	LightingZones = 4,
	Sensors = 8,
	Coolers = 16,
}
