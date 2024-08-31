using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
public struct FeatureAccessHeader
{
	public byte ReportId;
	public byte DeviceId;
	public byte FeatureIndex;
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	public byte FunctionAndSoftwareId;

	public byte FunctionId
	{
		get => (byte)(FunctionAndSoftwareId >> 4);
		set => FunctionAndSoftwareId = (byte)(FunctionAndSoftwareId & 0xF | value << 4);
	}

	public byte SoftwareId
	{
		get => (byte)(FunctionAndSoftwareId & 0xF);
		set => FunctionAndSoftwareId = (byte)(FunctionAndSoftwareId & 0xF0 | value & 0xF);
	}
}
