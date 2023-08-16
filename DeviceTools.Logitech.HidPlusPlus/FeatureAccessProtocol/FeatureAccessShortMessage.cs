using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 7)]
public struct FeatureAccessShortMessage<TParameters>
	where TParameters : struct, IShortMessageParameters
{
	public FeatureAccessHeader Header;
	public TParameters Parameters;

	public static implicit operator FeatureAccessShortMessage(in FeatureAccessShortMessage<TParameters> message)
		=> Unsafe.As<FeatureAccessShortMessage<TParameters>, FeatureAccessShortMessage>(ref Unsafe.AsRef(message));

	public static explicit operator FeatureAccessShortMessage<TParameters>(in FeatureAccessShortMessage message)
		=> Unsafe.As<FeatureAccessShortMessage, FeatureAccessShortMessage<TParameters>>(ref Unsafe.AsRef(message));
}

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 7)]
public struct FeatureAccessShortMessage
{
	public FeatureAccessHeader Header;
	public RawLongMessageParameters Parameters;
}
