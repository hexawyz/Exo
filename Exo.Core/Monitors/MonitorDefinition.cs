using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exo.Monitors;

public readonly struct MonitorDefinition
{
	public string? Name { get; init; }
	[JsonConverter(typeof(Utf8StringConverter))]
	public ImmutableArray<byte> Capabilities { get; init; }
	public ImmutableArray<MonitorFeatureDefinition> OverriddenFeatures { get; init; }
	public ImmutableArray<byte> IgnoredCapabilitiesVcpCodes { get; init; }
	public bool IgnoreAllCapabilitiesVcpCodes { get; init; }
}

public readonly struct MonitorFeatureDefinition
{
	public Guid? NameStringId { get; init; }
	public required byte VcpCode { get; init; }
	public MonitorFeatureAccess Access { get; init; }
	public required MonitorFeature Feature { get; init; }
	public ImmutableArray<MonitorFeatureDiscreteValueDefinition> DiscreteValues { get; init; }
	public ushort? MinimumValue { get; init; }
	public ushort? MaximumValue { get; init; }
}

public readonly struct MonitorFeatureDiscreteValueDefinition
{
	public required ushort Value { get; init; }
	public Guid? NameStringId { get; init; }
}

public enum MonitorFeature : byte
{
	Other = 0,
	Brightness,
	Contrast,
	AudioVolume,
	InputSelect,
}

public enum MonitorFeatureAccess : byte
{
	ReadWrite = 0,
	ReadOnly = 1,
	WriteOnly = 2,
}

internal sealed class Utf8StringConverter : JsonConverter<ImmutableArray<byte>>
{
	public override ImmutableArray<byte> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Null)
		{
			return default;
		}
		else if (reader.TokenType == JsonTokenType.String)
		{
			var buffer = new byte[reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length];
			int length = reader.CopyString(buffer);
			if (length < buffer.Length) Array.Resize(ref buffer, length);
			return ImmutableCollectionsMarshal.AsImmutableArray(buffer);
		}
		throw new InvalidDataException();
	}

	public override void Write(Utf8JsonWriter writer, ImmutableArray<byte> value, JsonSerializerOptions options) => throw new NotSupportedException();
}
