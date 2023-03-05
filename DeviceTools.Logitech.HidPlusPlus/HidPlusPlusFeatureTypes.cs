namespace DeviceTools.Logitech.HidPlusPlus;

[Flags]
public enum HidPlusPlusFeatureTypes : byte
{
	None = 0x00,
	ComplianceDeactivatable = 0x08,
	EngineeringDeactivatable = 0x10,
	Engineering = 0x20,
	Hidden = 0x40,
	Obsolete = 0x80,
}
