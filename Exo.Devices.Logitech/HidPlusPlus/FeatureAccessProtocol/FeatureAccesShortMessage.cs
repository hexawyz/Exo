using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Exo.Devices.Logitech.HidPlusPlus.FeatureAccessProtocol;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 7)]
public struct FeatureAccesShortMessage<TParameters>
	where TParameters : struct, IShortMessageParameters
{
	public FeatureAccessHeader Header;
	public TParameters Parameters;

	public static implicit operator FeatureAccesShortMessage(in FeatureAccesShortMessage<TParameters> message)
		=> Unsafe.As<FeatureAccesShortMessage<TParameters>, FeatureAccesShortMessage>(ref Unsafe.AsRef(message));

	public static explicit operator FeatureAccesShortMessage<TParameters>(in FeatureAccesShortMessage message)
		=> Unsafe.As<FeatureAccesShortMessage, FeatureAccesShortMessage<TParameters>>(ref Unsafe.AsRef(message));
}

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 7)]
public struct FeatureAccesShortMessage
{
	public FeatureAccessHeader Header;
	public RawLongMessageParameters Parameters;
}

