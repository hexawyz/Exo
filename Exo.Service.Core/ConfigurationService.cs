using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exo.Service;

public class ConfigurationService
{
	private const string DevicesRootDirectory = "dev";

	private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
	{
		AllowTrailingCommas = false,
		WriteIndented = false,
		NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
	};

	private readonly AsyncLock _lock;
	private readonly string _directory;
	private readonly string _devicesDirectory;

	public ConfigurationService(string directory)
	{
		directory = Path.GetFullPath(directory);
		var devicesDirectory = Path.Combine(directory, DevicesRootDirectory);
		Directory.CreateDirectory(directory);
		_lock = new();
		_directory = directory;
		_devicesDirectory = devicesDirectory;
	}

	public async ValueTask<T> ReadDeviceConfigurationAsync<T>(Guid deviceId, T defaultValue, CancellationToken cancellationToken)
	{
		var typeId = TypeId.Get<T>();

		using (await _lock.WaitAsync(cancellationToken))
		{
			string fileName = Path.Combine(_devicesDirectory, deviceId.ToString("B"), typeId.ToString("B")) + ".json";

			if (File.Exists(fileName))
			{
				using (var file = File.OpenRead(fileName))
				{
					var result = await JsonSerializer.DeserializeAsync<T>(file, JsonSerializerOptions, cancellationToken).ConfigureAwait(false);

					if (result is null) throw new ArgumentOutOfRangeException(nameof(result));

					return result;
				}
			}
			return defaultValue;
		}
	}

	public async ValueTask<T> WriteDeviceConfigurationAsync<T>(Guid deviceId, T defaultValue, CancellationToken cancellationToken)
	{
		var typeId = TypeId.Get<T>();

		using (await _lock.WaitAsync(cancellationToken))
		{
			string fileName = Path.Combine(_devicesDirectory, deviceId.ToString("B"), typeId.ToString("B")) + ".json";

			if (File.Exists(fileName))
			{
				using (var file = File.OpenWrite(fileName))
				{
					var result = await JsonSerializer.DeserializeAsync<T>(file, JsonSerializerOptions, cancellationToken).ConfigureAwait(false);

					if (result is null) throw new ArgumentOutOfRangeException(nameof(result));

					return result;
				}
			}
			return defaultValue;
		}
	}

	public async ValueTask DeleteDeviceConfigurationAsync(Guid deviceId)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			Directory.Delete(Path.Combine(_devicesDirectory, deviceId.ToString("B")), true);
		}
	}

	public async ValueTask DeleteDeviceConfigurationAsync<T>(Guid deviceId)
	{
		var typeId = TypeId.Get<T>();

		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			File.Delete(Path.Combine(_devicesDirectory, deviceId.ToString("B"), typeId.ToString("B")) + ".json");
		}
	}

	/// <summary>Exports the whole configuration in a zip archive format.</summary>
	/// <param name="stream"></param>
	/// <returns></returns>
	/// <exception cref="NotImplementedException"></exception>
	public async Task BackupAsync(Stream stream)
	{
		throw new NotImplementedException();
	}
}
