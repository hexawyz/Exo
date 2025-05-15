using System.Text.Json;
using System.Text.Json.Serialization;
using Exo.Primitives;

namespace Exo.Service.Configuration;

[TypeId(0x7757FFB0, 0x6111, 0x4DB1, 0xBC, 0xFC, 0x70, 0x97, 0x38, 0xF3, 0xC6, 0x34)]
[JsonConverter(typeof(JsonConverter))]
internal readonly struct PersistedSensorInformation
{
	// Custom serializer to ensure that min/max values are serialized using the proper format.
	// Perhaps another way is possible using generic types, however deserialization would always be somewhat of a problem.
	internal sealed class JsonConverter : JsonConverter<PersistedSensorInformation>
	{
		public override PersistedSensorInformation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

			reader.Read();
			if (reader.TokenType != JsonTokenType.PropertyName && reader.GetString() != nameof(PersistedSensorInformation.DataType)) throw new JsonException();

			reader.Read();
			var dataType = JsonSerializer.Deserialize<SensorDataType>(ref reader, options);

			reader.Read();
			if (reader.TokenType != JsonTokenType.PropertyName && reader.GetString() != nameof(PersistedSensorInformation.UnitSymbol)) throw new JsonException();

			reader.Read();
			string unitSymbol = reader.GetString() ?? throw new JsonException();

			SensorCapabilities capabilities = SensorCapabilities.None;
			reader.Read();
			if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();

			switch (reader.GetString())
			{
			// Backwards compatibility stuff.
			case "IsPolled":
				reader.Read();
				if (reader.GetBoolean()) capabilities |= SensorCapabilities.Polled;
				break;
			case nameof(PersistedSensorInformation.Capabilities):
				reader.Read();
				capabilities = JsonSerializer.Deserialize<SensorCapabilities>(ref reader, options);
				break;
			default: throw new JsonException();
			}

			VariantNumber maxValue = default;
			VariantNumber minValue = default;
			reader.Read();
			if (reader.TokenType == JsonTokenType.EndObject) goto Complete;
			if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();
			switch (reader.GetString())
			{
			case nameof(PersistedSensorInformation.ScaleMinimumValue):
				reader.Read();
				minValue = ReadNumericValue(ref reader, dataType, options);
				capabilities |= SensorCapabilities.HasMinimumValue;
				reader.Read();
				if (reader.TokenType == JsonTokenType.EndObject) goto Complete;
				if (reader.TokenType != JsonTokenType.PropertyName && reader.GetString() != nameof(PersistedSensorInformation.ScaleMaximumValue)) throw new JsonException();
				goto case nameof(PersistedSensorInformation.ScaleMaximumValue);
			case nameof(PersistedSensorInformation.ScaleMaximumValue):
				reader.Read();
				maxValue = ReadNumericValue(ref reader, dataType, options);
				capabilities |= SensorCapabilities.HasMaximumValue;
				reader.Read();
				if (reader.TokenType == JsonTokenType.EndObject) goto Complete;
				goto default;
			default:
				throw new JsonException();
			}
		Complete:;
			return new()
			{
				DataType = dataType,
				UnitSymbol = unitSymbol,
				Capabilities = capabilities,
				ScaleMinimumValue = minValue,
				ScaleMaximumValue = maxValue,
			};
		}

		private static VariantNumber ReadNumericValue(ref Utf8JsonReader reader, SensorDataType dataType, JsonSerializerOptions options)
			=> dataType switch
			{
				SensorDataType.UInt8 => reader.GetByte(),
				SensorDataType.UInt16 => reader.GetUInt16(),
				SensorDataType.UInt32 => reader.GetUInt32(),
				SensorDataType.UInt64 => reader.GetUInt64(),
				SensorDataType.UInt128 => JsonSerializer.Deserialize<UInt128>(ref reader, options),
				SensorDataType.SInt8 => reader.GetSByte(),
				SensorDataType.SInt16 => reader.GetInt16(),
				SensorDataType.SInt32 => reader.GetInt32(),
				SensorDataType.SInt64 => reader.GetInt64(),
				SensorDataType.SInt128 => JsonSerializer.Deserialize<Int128>(ref reader, options),
				SensorDataType.Float16 => JsonSerializer.Deserialize<Half>(ref reader, options),
				SensorDataType.Float32 => reader.GetSingle(),
				SensorDataType.Float64 => reader.GetDouble(),
				_ => throw new InvalidOperationException(),
			};

		private static void WriteNumericProperty(Utf8JsonWriter writer, SensorDataType dataType, string propertyName, VariantNumber value, JsonSerializerOptions options)
		{
			writer.WritePropertyName(propertyName);
			switch (dataType)
			{
			case SensorDataType.UInt8: writer.WriteNumberValue((byte)value); break;
			case SensorDataType.UInt16: writer.WriteNumberValue((ushort)value); break;
			case SensorDataType.UInt32: writer.WriteNumberValue((uint)value); break;
			case SensorDataType.UInt64: writer.WriteNumberValue((ulong)value); break;
			case SensorDataType.UInt128: JsonSerializer.Serialize(writer, (UInt128)value, options); break;
			case SensorDataType.SInt8: writer.WriteNumberValue((sbyte)value); break;
			case SensorDataType.SInt16: writer.WriteNumberValue((short)value); break;
			case SensorDataType.SInt32: writer.WriteNumberValue((int)value); break;
			case SensorDataType.SInt64: writer.WriteNumberValue((long)value); break;
			case SensorDataType.SInt128: JsonSerializer.Serialize(writer, (Int128)value, options); break;
			case SensorDataType.Float16: JsonSerializer.Serialize(writer, (Half)value, options); break;
			case SensorDataType.Float32: writer.WriteNumberValue((float)value); break;
			case SensorDataType.Float64: writer.WriteNumberValue((double)value); break;
			default: throw new InvalidOperationException();
			}
			;
		}

		public override void Write(Utf8JsonWriter writer, PersistedSensorInformation value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();
			writer.WritePropertyName(nameof(PersistedSensorInformation.DataType));
			JsonSerializer.Serialize(writer, value.DataType, options);
			writer.WriteString(nameof(PersistedSensorInformation.UnitSymbol), value.UnitSymbol);
			writer.WritePropertyName(nameof(PersistedSensorInformation.Capabilities));
			JsonSerializer.Serialize(writer, value.Capabilities, options);
			if ((value.Capabilities & SensorCapabilities.HasMinimumValue) != 0)
			{
				WriteNumericProperty(writer, value.DataType, nameof(PersistedSensorInformation.ScaleMinimumValue), value.ScaleMinimumValue, options);
			}
			if ((value.Capabilities & SensorCapabilities.HasMaximumValue) != 0)
			{
				WriteNumericProperty(writer, value.DataType, nameof(PersistedSensorInformation.ScaleMaximumValue), value.ScaleMaximumValue, options);
			}
			writer.WriteEndObject();
		}
	}

	public PersistedSensorInformation(SensorInformation info)
	{
		DataType = info.DataType;
		UnitSymbol = info.Unit;
		Capabilities = info.Capabilities;
		ScaleMinimumValue = info.ScaleMinimumValue;
		ScaleMaximumValue = info.ScaleMaximumValue;
	}

	public SensorDataType DataType { get; init; }
	public string UnitSymbol { get; init; }
	public SensorCapabilities Capabilities { get; init; }
	public VariantNumber ScaleMinimumValue { get; init; }
	public VariantNumber ScaleMaximumValue { get; init; }
}
