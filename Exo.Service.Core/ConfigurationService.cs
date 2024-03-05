using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exo.Service;

public readonly struct ConfigurationResult<T>
{
	public T? Value { get; }
	public ConfigurationStatus Status { get; }

	internal ConfigurationResult(ConfigurationStatus status)
	{
		Value = default!;
		Status = status;
	}

	internal ConfigurationResult(T value)
	{
		Value = value;
		Status = ConfigurationStatus.Found;
	}

	public bool Found => Status == ConfigurationStatus.Found;

	public void ThrowIfNotFound()
	{
		var status = Status;
		switch (status)
		{
		case ConfigurationStatus.Found: return;
		case ConfigurationStatus.MissingContainer: throw new InvalidOperationException("Missing configuration container.");
		case ConfigurationStatus.MissingValue: throw new InvalidOperationException("Missing configuration value.");
		case ConfigurationStatus.InvalidValue: throw new InvalidOperationException("Invalid configuration value.");
		default: throw new InvalidOperationException();
		}
	}
}

public enum ConfigurationStatus : sbyte
{
	Found = 0,
	MissingContainer = 1,
	MissingValue = 2,
	InvalidValue = 3,
}

public class ConfigurationService
{
	private const string DevicesRootDirectory = "dev";
	private const string AssembliesRootDirectory = "asm";

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
	private readonly string _assembliesDirectory;

	public ConfigurationService(string directory)
	{
		directory = Path.GetFullPath(directory);
		var devicesDirectory = Path.Combine(directory, DevicesRootDirectory);
		var assembliesDirectory = Path.Combine(directory, AssembliesRootDirectory);
		Directory.CreateDirectory(directory);
		Directory.CreateDirectory(devicesDirectory);
		_lock = new();
		_directory = directory;
		_devicesDirectory = devicesDirectory;
		_assembliesDirectory = assembliesDirectory;
	}

	private async ValueTask<ConfigurationResult<T>> ReadConfigurationAsync<T>(string directory, string key, CancellationToken cancellationToken)
	{
		string typeId = TypeId.GetString<T>();

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			string configurationDirectory = Path.Combine(directory, key);

			if (!Directory.Exists(configurationDirectory))
			{
				return new(ConfigurationStatus.MissingContainer);
			}

			string fileName = Path.Combine(configurationDirectory, typeId) + ".json";

			if (File.Exists(fileName))
			{
				using var file = File.OpenRead(fileName);
				var result = await JsonSerializer.DeserializeAsync<T>(file, JsonSerializerOptions, cancellationToken).ConfigureAwait(false);

				return result is null ? new(ConfigurationStatus.InvalidValue) : new(result);
			}
			return new(ConfigurationStatus.MissingValue);
		}
	}

	private async ValueTask WriteConfigurationAsync<T>(string directory, string key, T value, CancellationToken cancellationToken)
	{
		string typeId = TypeId.GetString<T>();
		if (value is null) throw new ArgumentOutOfRangeException(nameof(value));

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			string deviceDirectory = Path.Combine(directory, key);

			Directory.CreateDirectory(deviceDirectory);

			string fileName = Path.Combine(deviceDirectory, typeId) + ".json";

			using var file = File.Create(fileName);
			await JsonSerializer.SerializeAsync(file, value, JsonSerializerOptions, cancellationToken).ConfigureAwait(false);
		}
	}

	private async ValueTask DeleteConfigurationAsync(string directory, string key)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			Directory.Delete(Path.Combine(directory, key), true);
		}
	}

	private async ValueTask DeleteConfigurationAsync<T>(string directory, string key)
	{
		string typeId = TypeId.GetString<T>();

		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			File.Delete(Path.Combine(directory, key, typeId) + ".json");
		}
	}

	public async ValueTask<Guid[]> GetDevicesAsync(CancellationToken cancellationToken)
	{
		string[] directoryNames;
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			directoryNames = Directory.GetDirectories(_devicesDirectory, "????????-????-????-????-????????????", SearchOption.TopDirectoryOnly);
		}
		try
		{
			return Array.ConvertAll(directoryNames, n => Guid.ParseExact(Path.GetFileName(n.AsSpan()), "D"));
		}
		catch (Exception ex)
		{
			// TODO: Log
			return ParseDeviceIdsSlow(directoryNames);
		}
	}

	private Guid[] ParseDeviceIdsSlow(string[] directoryNames)
	{
		var deviceIds = new List<Guid>(directoryNames.Length);
		foreach (var directoryName in directoryNames)
		{
			if (Guid.TryParseExact(Path.GetFileName(directoryName.AsSpan()), "D", out Guid deviceId))
			{
				deviceIds.Add(deviceId);
			}
		}
		return [.. deviceIds];
	}

	public ValueTask<ConfigurationResult<T>> ReadDeviceConfigurationAsync<T>(Guid deviceId, CancellationToken cancellationToken)
		=> ReadConfigurationAsync<T>(_devicesDirectory, deviceId.ToString("D"), cancellationToken)!;

	public ValueTask WriteDeviceConfigurationAsync<T>(Guid deviceId, T value, CancellationToken cancellationToken)
		where T : notnull
		=> WriteConfigurationAsync(_devicesDirectory, deviceId.ToString("D"), value, cancellationToken);

	public ValueTask DeleteDeviceConfigurationAsync(Guid deviceId)
		=> DeleteConfigurationAsync(_devicesDirectory, deviceId.ToString("D"));

	public ValueTask DeleteDeviceConfigurationAsync<T>(Guid deviceId)
		=> DeleteConfigurationAsync<T>(_devicesDirectory, deviceId.ToString("D"));


	public ValueTask<ConfigurationResult<T>> ReadAssemblyConfigurationAsync<T>(AssemblyName assemblyName, CancellationToken cancellationToken)
		=> ReadConfigurationAsync<T>(_assembliesDirectory, assemblyName.FullName, cancellationToken);

	public ValueTask WriteAssemblyConfigurationAsync<T>(AssemblyName assemblyName, T value, CancellationToken cancellationToken)
		=> WriteConfigurationAsync(_assembliesDirectory, assemblyName.FullName, value, cancellationToken);

	public ValueTask DeleteAssemblyConfigurationAsync(AssemblyName assemblyName)
		=> DeleteConfigurationAsync(_assembliesDirectory, assemblyName.FullName);

	public ValueTask DeleteAssemblyConfigurationAsync<T>(AssemblyName assemblyName)
		=> DeleteConfigurationAsync<T>(_assembliesDirectory, assemblyName.FullName);

	/// <summary>Exports the whole configuration in a zip archive format.</summary>
	/// <param name="stream"></param>
	/// <returns></returns>
	/// <exception cref="NotImplementedException"></exception>
	public async Task BackupAsync(Stream stream)
	{
		throw new NotImplementedException();
	}
}
