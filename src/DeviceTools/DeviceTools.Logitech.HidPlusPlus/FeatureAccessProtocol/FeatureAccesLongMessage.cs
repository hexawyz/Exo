using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
public struct FeatureAccessLongMessage<TParameters>
	where TParameters : struct, ILongMessageParameters
{
	public FeatureAccessHeader Header;
	public TParameters Parameters;

	public static implicit operator FeatureAccessLongMessage(in FeatureAccessLongMessage<TParameters> message)
		=> Unsafe.As<FeatureAccessLongMessage<TParameters>, FeatureAccessLongMessage>(ref Unsafe.AsRef(in message));

	public static explicit operator FeatureAccessLongMessage<TParameters>(in FeatureAccessLongMessage message)
		=> Unsafe.As<FeatureAccessLongMessage, FeatureAccessLongMessage<TParameters>>(ref Unsafe.AsRef(in message));
}

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
public struct FeatureAccessLongMessage
{
	public FeatureAccessHeader Header;
	public RawLongMessageParameters Parameters;
}
