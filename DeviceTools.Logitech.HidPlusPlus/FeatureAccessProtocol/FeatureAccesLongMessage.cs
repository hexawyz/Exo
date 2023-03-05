using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
public struct FeatureAccesLongMessage<TParameters>
	where TParameters : struct, ILongMessageParameters
{
	public FeatureAccessHeader Header;
	public TParameters Parameters;

	public static implicit operator FeatureAccesLongMessage(in FeatureAccesLongMessage<TParameters> message)
		=> Unsafe.As<FeatureAccesLongMessage<TParameters>, FeatureAccesLongMessage>(ref Unsafe.AsRef(message));

	public static explicit operator FeatureAccesLongMessage<TParameters>(in FeatureAccesLongMessage message)
		=> Unsafe.As<FeatureAccesLongMessage, FeatureAccesLongMessage<TParameters>>(ref Unsafe.AsRef(message));
}

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
public struct FeatureAccesLongMessage
{
	public FeatureAccessHeader Header;
	public RawLongMessageParameters Parameters;
}
