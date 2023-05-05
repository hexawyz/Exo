namespace DeviceTools.Firmware;

public enum CpuStatus : byte
{
	/// <summary>Unknown status.</summary>
	Unknown = 0,
	/// <summary>CPU is enabled.</summary>
	Enabled = 1,
	/// <summary>CPU is disabled by user through BIOS Setup.</summary>
	DisabledByUser = 2,
	/// <summary>CPU is disabled by BIOS (POST error).</summary>
	DisabledByBios = 3,
	/// <summary>CPU is idle, waiting to be enabled.</summary>
	Idle = 4,
	/// <summary>Other status.</summary>
	Other = 7,
}
