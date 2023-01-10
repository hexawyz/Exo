using System.Buffers.Binary;

namespace Exo.Devices.Logitech.HidPlusPlus;

public struct HidPlusPlusGetFeatureParameters : IMessageParameters
{
	private ushort _featureId;

	public ushort FeatureId
	{
		get => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(_featureId) : _featureId;
		set => _featureId = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
	}
}
