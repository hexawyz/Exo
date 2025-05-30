using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exo.Metadata;

if (args.Length < 3)
{
	Console.Error.WriteLine("Usage: MetadataCompiler [category] [input] [output]");
	return;
}

var jsonSerializerOptions = new JsonSerializerOptions()
{
	PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	Converters =
	{
		new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false),
	},
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	ReadCommentHandling = JsonCommentHandling.Skip,
};

using var sourceFile = File.OpenRead(args[1]);
var builder = new InMemoryExoArchiveBuilder();

// NB: For now, we're not handling driver key and compatibility ID hierarchy for things that could be device specific.
// Need to see how the JSON would/could look like for this. (Do not want to make the default simple JSON too complex)
switch (args[0])
{
case "strings":
	{
		if (await JsonSerializer.DeserializeAsync<Dictionary<Guid, Dictionary<string, string>>>(sourceFile, jsonSerializerOptions) is not { } stringData)
		{
			throw new InvalidDataException();
		}
		// At the obvious expense of more CPU time being spent in this processor, we will deduplicate strings in a given file.
		// As string files grow larger it is expected to have a few redundant values, and it will be great if we can shave off a few hundred of bytes from doing this.
		// Reasons for duplication:
		// - Two string IDs with a somewhat different semantic meaning actually end up using the same contents for most (or all ?) languages.
		// - Two languages use the same word(s) to define something
		// - Any variation of or between the above.
		var deduplicatedStrings = new Dictionary<string, InMemoryExoArchiveBuilder.FileReference>(StringComparer.Ordinal);
		byte[] keyBuffer = new byte[16 + 11];
		keyBuffer[16] = (byte)'/';
		foreach (var kvp1 in stringData)
		{
			foreach (var kvp2 in kvp1.Value)
			{
				// Validate the culture name referenced in the data.
				var culture = CultureInfo.GetCultureInfo(kvp2.Key);
				kvp1.Key.TryWriteBytes(keyBuffer);
				int keyLength = 16;
				if (culture.Name.Length > 0)
				{
					keyLength++;
					keyLength += Encoding.UTF8.GetBytes(culture.Name, keyBuffer.AsSpan(keyLength));
				}
				if (deduplicatedStrings.TryGetValue(kvp2.Value, out var file))
				{
					builder.AddFile(keyBuffer.AsSpan(0, keyLength), file);
				}
				else
				{
					deduplicatedStrings.Add(kvp2.Value, builder.AddFile(keyBuffer.AsSpan(0, keyLength), Encoding.UTF8.GetBytes(kvp2.Value)));
				}
			}
		}
		break;
	}
case "sensors":
	{
		if (await JsonSerializer.DeserializeAsync<Dictionary<Guid, SensorMetadata>>(sourceFile, jsonSerializerOptions) is not { } sensorMetadata)
		{
			throw new InvalidDataException();
		}

		byte[] keyBuffer = new byte[16];
		foreach (var kvp in sensorMetadata)
		{
			kvp.Key.TryWriteBytes(keyBuffer);
			builder.AddFile(keyBuffer, MetadataSerializer.Serialize(kvp.Value));
		}
	}
	break;
case "coolers":
	{
		if (await JsonSerializer.DeserializeAsync<Dictionary<Guid, CoolerMetadata>>(sourceFile, jsonSerializerOptions) is not { } sensorMetadata)
		{
			throw new InvalidDataException();
		}

		byte[] keyBuffer = new byte[16];
		foreach (var kvp in sensorMetadata)
		{
			kvp.Key.TryWriteBytes(keyBuffer);
			builder.AddFile(keyBuffer, MetadataSerializer.Serialize(kvp.Value));
		}
	}
	break;
case "lighting-effects":
	{
		if (await JsonSerializer.DeserializeAsync<Dictionary<Guid, LightingEffectMetadata>>(sourceFile, jsonSerializerOptions) is not { } lightingEffectMetadata)
		{
			throw new InvalidDataException();
		}

		byte[] keyBuffer = new byte[16];
		foreach (var kvp in lightingEffectMetadata)
		{
			kvp.Key.TryWriteBytes(keyBuffer);
			builder.AddFile(keyBuffer, MetadataSerializer.Serialize(kvp.Value));
		}
	}
	break;
case "lighting-zones":
	{
		if (await JsonSerializer.DeserializeAsync<Dictionary<Guid, LightingZoneMetadata>>(sourceFile, jsonSerializerOptions) is not { } lightingZoneMetadata)
		{
			throw new InvalidDataException();
		}

		byte[] keyBuffer = new byte[16];
		foreach (var kvp in lightingZoneMetadata)
		{
			kvp.Key.TryWriteBytes(keyBuffer);
			builder.AddFile(keyBuffer, MetadataSerializer.Serialize(kvp.Value));
		}
	}
	break;
default:
	Console.Error.WriteLine($"Unknown category: {args[0]}.");
	return;
}

await builder.SaveAsync(args[2], default);
