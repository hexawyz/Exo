using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exo.Monitors;

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
