using System.Collections.Immutable;

namespace DeviceTools.Firmware.Uefi;

public readonly struct EfiVariableValue
{
	public EfiVariableValue(ImmutableArray<byte> value, EfiVariableAttributes attributes)
	{
		Value = value;
		Attributes = attributes;
	}

	public ImmutableArray<byte> Value { get; }
	public EfiVariableAttributes Attributes { get; }
}
