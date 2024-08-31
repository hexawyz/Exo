using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
public struct RegisterAccessHeader
{
	public byte ReportId;
	public byte DeviceId;
	private byte _subId;

	public SubId SubId
	{
		get => (SubId)_subId;
		set => _subId = (byte)value;
	}
}
