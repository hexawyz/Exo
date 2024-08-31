namespace Exo.Lighting;

/// <summary>Indicates the degree of support for lighting persistence of the device.</summary>
public enum LightingPersistenceMode : byte
{
	/// <summary>Indicates that lighting changes can not be persisted on the device.</summary>
	NeverPersisted = 0,
	/// <summary>Indicates that lighting changes can optionally be persisted on the device.</summary>
	CanPersist = 1,
	/// <summary>Indicates that all lighting changes will be persisted on the device.</summary>
	AlwaysPersisted = 2,
}
