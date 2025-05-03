using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exo.ColorFormats;
using Exo.Lighting.Effects;

namespace Exo.Lighting;

/// <summary>Information on a property that can be configured through the UI.</summary>
[JsonConverter(typeof(JsonConverter))]
public sealed class ConfigurablePropertyInformation : IEquatable<ConfigurablePropertyInformation?>
{
	private sealed class JsonConverter : JsonConverter<ConfigurablePropertyInformation>
	{
		public override ConfigurablePropertyInformation? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

			reader.Read();

			if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();

			string? name = null;
			string? displayName = null;
			LightingDataType dataType = LightingDataType.Other;
			object? defaultValue = null;
			object? minimumValue = null;
			object? maximumValue = null;
			ImmutableArray<EnumerationValue> enumerationValues = default;
			uint minimumElementCount = 1;
			uint maximumElementCount = 1;
			while (true)
			{
				string? propertyName = reader.GetString();
				reader.Read();
				switch (propertyName)
				{
				case nameof(name):
					name = reader.GetString();
					break;
				case nameof(displayName):
					displayName = reader.GetString() ?? throw new JsonException();
					break;
				case nameof(dataType):
					dataType = JsonSerializer.Deserialize<LightingDataType>(ref reader, options);
					break;
				case nameof(minimumElementCount):
					minimumElementCount = reader.GetUInt32();
					break;
				case nameof(maximumElementCount):
					maximumElementCount = reader.GetUInt32();
					break;
				case nameof(defaultValue):
					defaultValue = ReadValueOrValues(ref reader, dataType, options);
					break;
				case nameof(minimumValue):
					minimumValue = ReadValueOrValues(ref reader, dataType, options);
					break;
				case nameof(maximumValue):
					maximumValue = ReadValueOrValues(ref reader, dataType, options);
					break;
				case nameof(enumerationValues):
					enumerationValues = JsonSerializer.Deserialize<ImmutableArray<EnumerationValue>>(ref reader, options);
					break;
				default:
					JsonSerializer.Deserialize<object?>(ref reader, options);
					break;
				}
				reader.Read();
				if (reader.TokenType == JsonTokenType.EndObject) break;
				else if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();
			}

			return new()
			{
				Name = name ?? throw new JsonException("Missing required Name property."),
				DisplayName = displayName ?? throw new JsonException("Missing required DisplayName property."),
				DataType = dataType != LightingDataType.Other ? dataType : throw new JsonException("Missing required DisplayName property"),
				DefaultValue = defaultValue,
				MinimumValue = minimumValue,
				MaximumValue = maximumValue,
				EnumerationValues = enumerationValues,
				MinimumElementCount = minimumElementCount,
				MaximumElementCount = maximumElementCount,
			};
		}

		private static object? ReadValueOrValues(ref Utf8JsonReader reader, LightingDataType dataType, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.StartArray)
			{
				return ReadValues(ref reader, dataType, options);
			}
			return ReadValue(ref reader, dataType, options);
		}

		private static object? ReadValue(ref Utf8JsonReader reader, LightingDataType dataType, JsonSerializerOptions options)
			=> dataType switch
			{
				LightingDataType.Other => throw new Exception("DataType has not been defined."),
				LightingDataType.UInt8 => reader.GetByte(),
				LightingDataType.SInt8 => reader.GetSByte(),
				LightingDataType.UInt16 => reader.GetUInt16(),
				LightingDataType.SInt16 => reader.GetInt16(),
				LightingDataType.UInt32 => reader.GetUInt32(),
				LightingDataType.SInt32 => reader.GetInt32(),
				LightingDataType.UInt64 => reader.GetUInt64(),
				LightingDataType.SInt64 => reader.GetInt64(),
				LightingDataType.Float16 => (Half)reader.GetSingle(),
				LightingDataType.Float32 => reader.GetSingle(),
				LightingDataType.Float64 => reader.GetDouble(),
				LightingDataType.Boolean => reader.GetBoolean(),
				LightingDataType.Guid => reader.GetGuid(),
				LightingDataType.TimeSpan => throw new NotImplementedException("TODO"),
				LightingDataType.DateTime => reader.GetDateTime(),
				LightingDataType.String => reader.GetString(),
				LightingDataType.EffectDirection1D => JsonSerializer.Deserialize<EffectDirection1D>(ref reader, options),
				LightingDataType.ColorRgb24 => RgbColor.Parse(reader.GetString(), CultureInfo.InvariantCulture),
				_ => throw new NotImplementedException(),
			};

		private static object? ReadValues(ref Utf8JsonReader reader, LightingDataType dataType, JsonSerializerOptions options)
		{
			reader.Read();
			switch (dataType)
			{
			case LightingDataType.Other: throw new Exception("DataType has not been defined.");
			case LightingDataType.UInt8:
				{
					var values = new List<byte>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetByte());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.SInt8:
				{
					var values = new List<sbyte>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetSByte());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.UInt16:
				{
					var values = new List<ushort>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetUInt16());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.SInt16:
				{
					var values = new List<short>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetInt16());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.UInt32:
				{
					var values = new List<uint>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetUInt32());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.SInt32:
				{
					var values = new List<int>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetInt32());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.UInt64:
				{
					var values = new List<ulong>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetUInt64());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.SInt64:
				{
					var values = new List<long>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetInt64());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.Float16:
				{
					var values = new List<Half>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add((Half)reader.GetSingle());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.Float32:
				{
					var values = new List<float>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetSingle());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.Float64:
				{
					var values = new List<double>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetDouble());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.Boolean:
				{
					var values = new List<bool>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetBoolean());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.Guid:
				{
					var values = new List<Guid>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetGuid());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.TimeSpan: throw new NotImplementedException("TODO");
			case LightingDataType.DateTime:
				{
					var values = new List<DateTime>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetDateTime());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.String:
				{
					var values = new List<string?>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(reader.GetString());
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.EffectDirection1D:
				{
					var values = new List<EffectDirection1D>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(JsonSerializer.Deserialize<EffectDirection1D>(ref reader, options));
						reader.Read();
					}
					return values.ToArray();
				}
			case LightingDataType.ColorRgb24:
				{
					var values = new List<RgbColor>();
					while (reader.TokenType != JsonTokenType.EndArray)
					{
						values.Add(RgbColor.Parse(reader.GetString(), CultureInfo.InvariantCulture));
						reader.Read();
					}
					return values.ToArray();
				}
			default: throw new NotImplementedException();
			}
		}

		public override void Write(Utf8JsonWriter writer, ConfigurablePropertyInformation value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();
			writer.WriteString("name", value.Name);
			writer.WriteString("displayName", value.DisplayName);
			writer.WritePropertyName("dataType");
			JsonSerializer.Serialize(writer, value.DataType, options);
			if (value.IsArray)
			{
				writer.WriteNumber("minimumElementCount", value.MinimumElementCount);
				writer.WriteNumber("maximumElementCount", value.MaximumElementCount);
			}
			if (value.DefaultValue is not null)
			{
				writer.WritePropertyName("defaultValue");
				if (value.IsArray && value.DefaultValue is Array values)
				{
					WriteValues(writer, value.DataType, values, options);
				}
				else
				{
					WriteValue(writer, value.DataType, value.DefaultValue, options);
				}
			}
			if (value.MinimumValue is not null)
			{
				writer.WritePropertyName("minimumValue");
				if (value.IsArray && value.MinimumValue is Array values)
				{
					WriteValues(writer, value.DataType, values, options);
				}
				else
				{
					WriteValue(writer, value.DataType, value.MinimumValue, options);
				}
			}
			if (value.MaximumValue is not null)
			{
				writer.WritePropertyName("maximumValue");
				if (value.IsArray && value.MaximumValue is Array values)
				{
					WriteValues(writer, value.DataType, values, options);
				}
				else
				{
					WriteValue(writer, value.DataType, value.MaximumValue, options);
				}
			}
			if (!value.EnumerationValues.IsDefaultOrEmpty)
			{
				writer.WritePropertyName("enumerationValues");
				JsonSerializer.Serialize(writer, value.EnumerationValues, options);
			}
			writer.WriteEndObject();
		}

		private static void WriteValue(Utf8JsonWriter writer, LightingDataType dataType, object value, JsonSerializerOptions options)
		{
			switch (dataType)
			{
			case LightingDataType.Other: throw new Exception("DataType has not been defined.");
			case LightingDataType.UInt8: writer.WriteNumberValue((byte)value); break;
			case LightingDataType.SInt8: writer.WriteNumberValue((sbyte)value); break;
			case LightingDataType.UInt16: writer.WriteNumberValue((ushort)value); break;
			case LightingDataType.SInt16: writer.WriteNumberValue((short)value); break;
			case LightingDataType.UInt32: writer.WriteNumberValue((uint)value); break;
			case LightingDataType.SInt32: writer.WriteNumberValue((int)value); break;
			case LightingDataType.UInt64: writer.WriteNumberValue((ulong)value); break;
			case LightingDataType.SInt64: writer.WriteNumberValue((long)value); break;
			case LightingDataType.Float16: writer.WriteNumberValue((float)(Half)value); break;
			case LightingDataType.Float32: writer.WriteNumberValue((float)value); break;
			case LightingDataType.Float64: writer.WriteNumberValue((double)value); break;
			case LightingDataType.Boolean: writer.WriteBooleanValue((bool)value); break;
			case LightingDataType.Guid: writer.WriteStringValue((Guid)value); break;
			case LightingDataType.TimeSpan: throw new NotImplementedException("TODO");
			case LightingDataType.DateTime: writer.WriteStringValue((DateTime)value); break;
			case LightingDataType.String: writer.WriteStringValue((string)value); break;
			case LightingDataType.EffectDirection1D: JsonSerializer.Serialize(writer, (EffectDirection1D)value, options); break;
			case LightingDataType.ColorRgb24: writer.WriteStringValue(((RgbColor)value).ToString()); break;
			default: throw new InvalidOperationException("Unsupported DataType.");
			}
		}

		private static void WriteValues(Utf8JsonWriter writer, LightingDataType dataType, Array values, JsonSerializerOptions options)
		{
			writer.WriteStartArray();
			switch (dataType)
			{
			case LightingDataType.Other: throw new Exception("DataType has not been defined.");
			case LightingDataType.UInt8:
				foreach (var value in (byte[])values)
				{
					writer.WriteNumberValue(value);
				}
				break;
			case LightingDataType.SInt8:
				foreach (var value in (sbyte[])values)
				{
					writer.WriteNumberValue(value);
				}
				break;
			case LightingDataType.UInt16:
				foreach (var value in (ushort[])values)
				{
					writer.WriteNumberValue(value);
				}
				break;
			case LightingDataType.SInt16:
				foreach (var value in (short[])values)
				{
					writer.WriteNumberValue(value);
				}
				break;
			case LightingDataType.UInt32:
				foreach (var value in (uint[])values)
				{
					writer.WriteNumberValue(value);
				}
				break;
			case LightingDataType.SInt32:
				foreach (var value in (int[])values)
				{
					writer.WriteNumberValue(value);
				}
				break;
			case LightingDataType.UInt64:
				foreach (var value in (ulong[])values)
				{
					writer.WriteNumberValue(value);
				}
				break;
			case LightingDataType.SInt64:
				foreach (var value in (long[])values)
				{
					writer.WriteNumberValue(value);
				}
				break;
			case LightingDataType.Float16:
				foreach (var value in (Half[])values)
				{
					writer.WriteNumberValue((float)value);
				}
				break;
			case LightingDataType.Float32:
				foreach (var value in (float[])values)
				{
					writer.WriteNumberValue(value);
				}
				break;
			case LightingDataType.Float64:
				foreach (var value in (double[])values)
				{
					writer.WriteNumberValue(value);
				}
				break;
			case LightingDataType.Boolean:
				foreach (var value in (bool[])values)
				{
					writer.WriteBooleanValue(value);
				}
				break;
			case LightingDataType.Guid:
				foreach (var value in (Guid[])values)
				{
					writer.WriteStringValue(value);
				}
				break;
			case LightingDataType.TimeSpan: throw new NotImplementedException("TODO");
			case LightingDataType.DateTime:
				foreach (var value in (DateTime[])values)
				{
					writer.WriteStringValue(value);
				}
				break;
			case LightingDataType.String:
				foreach (var value in (string[])values)
				{
					writer.WriteStringValue(value);
				}
				break;
			case LightingDataType.EffectDirection1D:
				foreach (var value in (EffectDirection1D[])values)
				{
					JsonSerializer.Serialize(writer, value, options);
				}
				break;
			case LightingDataType.ColorRgb24:
				foreach (var value in (RgbColor[])values)
				{
					writer.WriteStringValue(value.ToString());
				}
				break;
			default: throw new InvalidOperationException("Unsupported DataType.");
			}
			writer.WriteEndArray();
		}
	}

	/// <summary>The name of the property.</summary>
	public required string Name { get; init; }

	// TODO: Replace all display name & similar stuff everywhere by a GUID that will be used for localization. Also, properties should probably have a display order.

	/// <summary>The display name of the property.</summary>
	public required string DisplayName { get; init; }

	/// <summary>The data type of the property.</summary>
	public required LightingDataType DataType { get; init; }

	/// <summary>The minimum number of elements for this property.</summary>
	/// <remarks>
	/// This value must be strictly positive and never greater than <see cref="MaximumElementCount"/>.
	/// If both <see cref="MinimumElementCount"/> and <see cref="MaximumElementCount"/> are <c>1</c> then the property is not an array.
	/// If both are equal, the property is a fixed array. Otherwise, it is a fixed array.
	/// </remarks>
	public required uint MinimumElementCount { get; init; }

	/// <summary>The maximum number of elements for this property.</summary>
	/// <remarks>
	/// This value must be strictly positive and never less than <see cref="MaximumElementCount"/>.
	/// If both <see cref="MinimumElementCount"/> and <see cref="MaximumElementCount"/> are <c>1</c> then the property is not an array.
	/// If both are equal, the property is a fixed array. Otherwise, it is a fixed array.
	/// </remarks>
	public required uint MaximumElementCount { get; init; }

	/// <summary>The default value of the property, if any.</summary>
	public object? DefaultValue { get; init; }

	/// <summary>The minimum value of the property, if applicable.</summary>
	public object? MinimumValue { get; init; }

	/// <summary>The maximum value of the property, if applicable.</summary>
	public object? MaximumValue { get; init; }

	/// <summary>The unit in which numeric values are expressed, if applicable.</summary>
	public string? Unit { get; init; }

	private readonly ImmutableArray<EnumerationValue> _enumerationValues = [];

	/// <summary>Determines the allowed values if the field is an enumeration.</summary>
	// NB: Might not be very efficient to have this here, since it would be replicated for every property if the same type is reused. Let's see how critical this is later.
	public required ImmutableArray<EnumerationValue> EnumerationValues
	{
		get => _enumerationValues;
		init => _enumerationValues = value.IsDefaultOrEmpty ? [] : value;
	}

	public bool IsArray => MinimumElementCount != 1 || MinimumElementCount != MaximumElementCount;
	public bool IsVariableLengthArray => MinimumElementCount != MaximumElementCount;
	public bool IsFixedLengthArray => MaximumElementCount != 1 && MinimumElementCount == MaximumElementCount;

	public override bool Equals(object? obj) => Equals(obj as ConfigurablePropertyInformation);

	public bool Equals(ConfigurablePropertyInformation? other)
		=> other is not null &&
			Name == other.Name &&
			DisplayName == other.DisplayName &&
			DataType == other.DataType &&
			Equals(DefaultValue, other.DefaultValue) &&
			Equals(MinimumValue, other.MinimumValue) &&
			Equals(MaximumValue, other.MaximumValue) &&
			Unit == other.Unit &&
			EnumerationValues.SequenceEqual(other.EnumerationValues) &&
			MinimumElementCount == other.MinimumElementCount &&
			MaximumElementCount == other.MaximumElementCount;

	public override int GetHashCode()
	{
		var hash = new HashCode();
		hash.Add(Name);
		hash.Add(DisplayName);
		hash.Add(DataType);
		hash.Add(DefaultValue);
		hash.Add(MinimumValue);
		hash.Add(MaximumValue);
		hash.Add(Unit);
		hash.Add(EnumerationValues.Length);
		hash.Add(MinimumElementCount);
		hash.Add(MaximumElementCount);
		return hash.ToHashCode();
	}

	public static bool operator ==(ConfigurablePropertyInformation? left, ConfigurablePropertyInformation? right) => EqualityComparer<ConfigurablePropertyInformation>.Default.Equals(left, right);
	public static bool operator !=(ConfigurablePropertyInformation? left, ConfigurablePropertyInformation? right) => !(left == right);
}
