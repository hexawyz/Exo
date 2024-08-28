namespace DeviceTools.Logitech.HidPlusPlus;

public readonly struct DpiStatus
{
	public byte? PresetIndex { get; }
	public DotsPerInch Dpi { get; }

	public DpiStatus(DotsPerInch dpi) => Dpi = dpi;
	public DpiStatus(byte presetIndex, DotsPerInch dpi) => (PresetIndex, Dpi) = (presetIndex, dpi);
}
