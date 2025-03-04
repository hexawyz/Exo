namespace Exo.PowerManagement;

/// <summary>Defines the various power settings for which notifications can be received.</summary>
[Flags]
public enum PowerSettings
{
	None = 0b_00000000_00000000,
	AcDcPowerSource = 0b_00000000_00000001,
	BatteryPercentageRemaining = 0b_00000000_00000010,
	ConsoleDisplayState = 0b_00000000_00000100,
	SessionDisplayStatus = 0b_00000000_00001000,
	GlobalUserPresence = 0b_00000000_00010000,
	SessionUserPresence = 0b_00000000_00100000,
	IdleBackgroundTask = 0b_00000000_01000000,
	LidSwitchStateChange = 0b_00000000_10000000,
	MonitorPowerOn = 0b_00000001_00000000,
	PowerSavingStatus = 0b_00000010_00000000,
	EnergySaverStatus = 0b_00000100_00000000,
	PowerSchemePersonality = 0b_00001000_00000000,
	SystemAwayMode = 0b_00010000_00000000,
}

public enum SystemPowerCondition
{
	Ac,
	Dc,
	Hot,
}

public enum MonitorDisplayState
{
	Off,
	On,
	Dim,
}

public enum UserActivityPresence
{
	Present,
	NotPresent,
	Inactive,
}

public enum EnergySaverStatus
{
	Off,
	Standard,
	HighSavings,
}
