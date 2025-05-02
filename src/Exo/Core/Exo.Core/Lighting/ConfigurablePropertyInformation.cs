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
			int? arrayLength = null;
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
				case nameof(defaultValue):
					defaultValue = ReadValue(ref reader, dataType, options);
					break;
				case nameof(minimumValue):
					minimumValue = ReadValue(ref reader, dataType, options);
					break;
				case nameof(maximumValue):
					maximumValue = ReadValue(ref reader, dataType, options);
					break;
				case nameof(enumerationValues):
					enumerationValues = JsonSerializer.Deserialize<ImmutableArray<EnumerationValue>>(ref reader, options);
					break;
				case nameof(arrayLength):
					arrayLength = reader.GetInt32();
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
				ArrayLength = arrayLength,
			};
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

		public override void Write(Utf8JsonWriter writer, ConfigurablePropertyInformation value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();
			writer.WriteString("name", value.Name);
			writer.WriteString("displayName", value.DisplayName);
			writer.WritePropertyName("dataType");
			JsonSerializer.Serialize(writer, value.DataType, options);
			if (value.DefaultValue is not null)
			{
				writer.WritePropertyName("defaultValue");
				WriteValue(writer, value.DataType, value.DefaultValue, options);
			}
			if (value.MinimumValue is not null)
			{
				writer.WritePropertyName("minimumValue");
				WriteValue(writer, value.DataType, value.MinimumValue, options);
			}
			if (value.MaximumValue is not null)
			{
				writer.WritePropertyName("maximumValue");
				WriteValue(writer, value.DataType, value.MaximumValue, options);
			}
			if (!value.EnumerationValues.IsDefaultOrEmpty)
			{
				writer.WritePropertyName("enumerationValues");
				JsonSerializer.Serialize(writer, value.EnumerationValues, options);
			}
			if (value.ArrayLength is not null) writer.WriteNumber("arrayLength", value.ArrayLength.GetValueOrDefault());
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
	}

	/// <summary>The name of the property.</summary>
	public required string Name { get; init; }

	// TODO: Replace all display name & similar stuff everywhere by a GUID that will be used for localization. Also, properties should probably have a display order.

	/// <summary>The display name of the property.</summary>
	public required string DisplayName { get; init; }

	/// <summary>The data type of the property.</summary>
	public required LightingDataType DataType { get; init; }

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

	/// <summary>The number of elements in the array, for fixed-length array data types.</summary>
	/// <remarks>Fixed-length arrays are the only kind of array supported. The array data will be materialized into <see cref="DataValue.BytesValue"/>.</remarks>
	public int? ArrayLength { get; init; }

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
			ArrayLength == other.ArrayLength;

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
		hash.Add(ArrayLength);
		return hash.ToHashCode();
	}

	public static bool operator ==(ConfigurablePropertyInformation? left, ConfigurablePropertyInformation? right) => EqualityComparer<ConfigurablePropertyInformation>.Default.Equals(left, right);
	public static bool operator !=(ConfigurablePropertyInformation? left, ConfigurablePropertyInformation? right) => !(left == right);
}
