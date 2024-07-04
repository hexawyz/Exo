using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceTools;
using Exo;
using Exo.Metadata;
using Exo.Monitors;

if (args.Length < 2)
{
	Console.Error.WriteLine("Usage: MonitorDatabaseCompiler [inputDir] [outputFile]");
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

var inputFileNames = Directory.GetFiles(args[0], "*.json");
var builder = new InMemoryExoArchiveBuilder();

byte[] keyBuffer = new byte[4];
foreach (var fileName in inputFileNames)
{
	int length = Path.GetFileName(fileName.AsSpan()).Length;
	int startIndex = fileName.Length - length;
	// Filename patterns are LLLNNNN[-LLLNNNN[-LLNNNN[…]]].json
	if (length != 12 && (length < 12 || (length - 12) % 8 != 0)) throw new Exception($"Invalid name format: {fileName}.");
	int endIndex = fileName.Length - 5;
	int currentIndex = startIndex;
	while (currentIndex < endIndex)
	{
		using var sourceFile = File.OpenRead(fileName);
		var definition = await JsonSerializer.DeserializeAsync<MonitorDefinition>(sourceFile, jsonSerializerOptions);
		var (vendorId, productId) = ParseMonitorId(fileName.AsSpan(currentIndex, 7));
		LittleEndian.Write(ref keyBuffer[0], vendorId.Value);
		LittleEndian.Write(ref keyBuffer[2], productId);
		builder.AddFile(keyBuffer.AsSpan(), MonitorDefinitionSerializer.Serialize(definition));
		currentIndex += 8;
	}
}

await builder.SaveAsync(args[1], default);

static (PnpVendorId VendorId, ushort productId) ParseMonitorId(ReadOnlySpan<char> text)
	=> (PnpVendorId.Parse(text[..3]), ushort.Parse(text.Slice(3, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
