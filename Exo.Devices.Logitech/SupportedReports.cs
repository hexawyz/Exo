namespace Exo.Devices.Logitech;

[Flags]
public enum SupportedReports : byte
{
	None = 0,
	Short = 1,
	Long = 2,
	VeryLong = 4,
}
