using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public enum MetadataArchiveCategory
{
	Strings = 0,
	LightingEffects = 1,
	LightingZones = 2,
	Sensors = 3,
	Coolers = 4,
}
