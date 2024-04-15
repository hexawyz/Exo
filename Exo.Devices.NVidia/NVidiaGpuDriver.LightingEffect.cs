namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	// GPUs that support "piecewise" effects could support more effects, but I don't have this on hand, so for now it is only disabled or static.
	private enum LightingEffect : byte
	{
		Disabled = 0,
		Static = 1,
	}
}
