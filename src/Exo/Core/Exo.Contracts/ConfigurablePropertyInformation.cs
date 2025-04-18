using System.Collections.Immutable;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exo.Contracts;

/// <summary>Information on a property that can be configured through the UI.</summary>
[DataContract]
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
			DataType dataType = DataType.Other;
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
					dataType = JsonSerializer.Deserialize<DataType>(ref reader, options);
					break;
				case nameof(defaultValue):
					defaultValue = ReadValue(ref reader, dataType);
					break;
				case nameof(minimumValue):
					minimumValue = ReadValue(ref reader, dataType);
					break;
				case nameof(maximumValue):
					maximumValue = ReadValue(ref reader, dataType);
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
				DataType = dataType != DataType.Other ? dataType : throw new JsonException("Missing required DisplayName property"),
				DefaultValue = defaultValue,
				MinimumValue = minimumValue,
				MaximumValue = maximumValue,
				EnumerationValues = enumerationValues,
				ArrayLength = arrayLength,
			};
		}

		private static object? ReadValue(ref Utf8JsonReader reader, DataType dataType)
			=> dataType switch
			{
				DataType.Other => throw new Exception("DataType has not been defined."),
				DataType.UInt8 => reader.GetByte(),
				DataType.Int8 => reader.GetSByte(),
				DataType.UInt16 => reader.GetUInt16(),
				DataType.Int16 => reader.GetInt16(),
				DataType.UInt32 => reader.GetUInt32(),
				DataType.Int32 => reader.GetInt32(),
				DataType.UInt64 => reader.GetUInt64(),
				DataType.Int64 => reader.GetInt64(),
				DataType.Float16 => (Half)reader.GetSingle(),
				DataType.Float32 => reader.GetSingle(),
				DataType.Float64 => reader.GetDouble(),
				DataType.Boolean => reader.GetBoolean(),
				DataType.Guid => reader.GetGuid(),
				DataType.TimeSpan => throw new NotImplementedException("TODO"),
				DataType.DateTime => reader.GetDateTime(),
				DataType.String => reader.GetString(),
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
				WriteValue(writer, value.DataType, value.DefaultValue);
			}
			if (value.MinimumValue is not null)
			{
				writer.WritePropertyName("minimumValue");
				WriteValue(writer, value.DataType, value.MinimumValue);
			}
			if (value.MaximumValue is not null)
			{
				writer.WritePropertyName("maximumValue");
				WriteValue(writer, value.DataType, value.MaximumValue);
			}
			if (!value.EnumerationValues.IsDefaultOrEmpty)
			{
				writer.WritePropertyName("enumerationValues");
				JsonSerializer.Serialize(writer, value.EnumerationValues, options);
			}
			if (value.ArrayLength is not null) writer.WriteNumber("arrayLength", value.ArrayLength.GetValueOrDefault());
			writer.WriteEndObject();
		}

		private static void WriteValue(Utf8JsonWriter writer, DataType dataType, object value)
		{
			switch (dataType)
			{
			case DataType.Other: throw new Exception("DataType has not been defined.");
			case DataType.UInt8: writer.WriteNumberValue((byte)value); break;
			case DataType.Int8: writer.WriteNumberValue((sbyte)value); break;
			case DataType.UInt16: writer.WriteNumberValue((ushort)value); break;
			case DataType.Int16: writer.WriteNumberValue((short)value); break;
			case DataType.UInt32: writer.WriteNumberValue((uint)value); break;
			case DataType.Int32: writer.WriteNumberValue((int)value); break;
			case DataType.UInt64: writer.WriteNumberValue((ulong)value); break;
			case DataType.Int64: writer.WriteNumberValue((long)value); break;
			case DataType.Float16: writer.WriteNumberValue((float)(Half)value); break;
			case DataType.Float32: writer.WriteNumberValue((float)value); break;
			case DataType.Float64: writer.WriteNumberValue((double)value); break;
			case DataType.Boolean: writer.WriteBooleanValue((bool)value); break;
			case DataType.Guid: writer.WriteStringValue((Guid)value); break;
			case DataType.TimeSpan: throw new NotImplementedException("TODO");
			case DataType.DateTime: writer.WriteStringValue((DateTime)value); break;
			case DataType.String: writer.WriteStringValue((string)value); break;
			default: throw new InvalidOperationException("Unsupported DataType.");
			}
		}
	}

	/// <summary>The name of the property.</summary>
	[DataMember(Order = 1)]
	public required string Name { get; init; }

	// TODO: Replace all display name & similar stuff everywhere by a GUID that will be used for localization. Also, properties should probably have a display order.

	/// <summary>The display name of the property.</summary>
	[DataMember(Order = 2)]
	public required string DisplayName { get; init; }

	/// <summary>The data type of the property.</summary>
	[DataMember(Order = 3)]
	public required DataType DataType { get; init; }

	/// <summary>The default value of the property, if any.</summary>
	[DataMember(Order = 4)]
	public object? DefaultValue { get; init; }

	/// <summary>The minimum value of the property, if applicable.</summary>
	[DataMember(Order = 5)]
	public object? MinimumValue { get; init; }

	/// <summary>The maximum value of the property, if applicable.</summary>
	[DataMember(Order = 6)]
	public object? MaximumValue { get; init; }

	/// <summary>The unit in which numeric values are expressed, if applicable.</summary>
	[DataMember(Order = 7)]
	public string? Unit { get; init; }

	private readonly ImmutableArray<EnumerationValue> _enumerationValues = [];

	/// <summary>Determines the allowed values if the field is an enumeration.</summary>
	// NB: Might not be very efficient to have this here, since it would be replicated for every property if the same type is reused. Let's see how critical this is later.
	[DataMember(Order = 8)]
	public required ImmutableArray<EnumerationValue> EnumerationValues
	{
		get => _enumerationValues;
		init => _enumerationValues = value.NotNull();
	}

	/// <summary>The number of elements in the array, for fixed-length array data types.</summary>
	/// <remarks>Fixed-length arrays are the only kind of array supported. The array data will be materialized into <see cref="DataValue.BytesValue"/>.</remarks>
	[DataMember(Order = 9)]
	public int? ArrayLength { get; init; }

	public override bool Equals(object? obj) => Equals(obj as ConfigurablePropertyInformation);

	public bool Equals(ConfigurablePropertyInformation? other)
		=> other is not null &&
			Name == other.Name &&
			DisplayName == other.DisplayName &&
			DataType == other.DataType &&
			DefaultValue == other.DefaultValue &&
			MinimumValue == other.MinimumValue &&
			MaximumValue == other.MaximumValue &&
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
