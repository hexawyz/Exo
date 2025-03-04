namespace Exo.PowerNotifications;

// Commented entries are documented as obsolete in the documentation (not supported in recent OSes)
internal enum PowerBroadcastStatus
{
	//QuerySuspend = 0x0000,
	//QuerySuspendFailed = 0x0002,
	Suspend = 0x0004,
	//ResumeCritical = 0x0006,
	ResumeSuspend = 0x0007,
	//BatteryLow = 0x0009,
	PowerStatusChange = 0x000A,
	//OemEvent = 0x000B,
	ResumeAutomatic = 0x0012,
	PowerSettingChange = 0x8013,
}
