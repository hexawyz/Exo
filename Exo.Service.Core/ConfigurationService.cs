using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exo.Configuration;

namespace Exo.Service;

public class ConfigurationService : IConfigurationNode
{
	private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
	{
		AllowTrailingCommas = false,
		WriteIndented = false,
		NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
	};

	private sealed class ConfigurationContainer : IConfigurationContainer
	{
		private readonly ConfigurationService _configurationService;
		private readonly string _directory;

		public ConfigurationContainer(ConfigurationService configurationService, string directoryName)
		{
			_configurationService = configurationService;
			_directory = directoryName;
		}

		public ValueTask<ConfigurationResult<TValue>> ReadValueAsync<TValue>(CancellationToken cancellationToken)
			=> _configurationService.ReadValueAsync<TValue>(_directory, null, cancellationToken);

		public ValueTask WriteValueAsync<TValue>(TValue value, CancellationToken cancellationToken)
			where TValue : notnull
			=> _configurationService.WriteValueAsync(_directory, null, value, cancellationToken);

		public ValueTask DeleteValueAsync<TValue>()
			=> _configurationService.DeleteValueAsync<TValue>(_directory);


		public IConfigurationContainer GetContainer(string containerName)
			=> new ConfigurationContainer(_configurationService, PrepareChildDirectory(_directory, containerName, false));

		public IConfigurationContainer<TKey> GetContainer<TKey>(string containerName, INameSerializer<TKey> nameSerializer)
			=> new ConfigurationContainer<TKey>(_configurationService, PrepareChildDirectory(_directory, containerName, false), nameSerializer);
	}

	private sealed class ConfigurationContainer<TKey> : IConfigurationContainer<TKey>
	{
		private readonly ConfigurationService _configurationService;
		private readonly string _directory;
		private readonly INameSerializer<TKey> _nameSerializer;

		public ConfigurationContainer(ConfigurationService configurationService, string directoryName, INameSerializer<TKey> nameSerializer)
		{
			_configurationService = configurationService;
			_directory = directoryName;
			_nameSerializer = nameSerializer;
		}

		public async ValueTask<TKey[]> GetKeysAsync(CancellationToken cancellationToken)
		{
			string[] directoryNames = await _configurationService.GetDirectoryNamesAsync(_directory, _nameSerializer.FileNamePattern, cancellationToken).ConfigureAwait(false);

			try
			{
				return Array.ConvertAll(directoryNames, n => _nameSerializer.Parse(Path.GetFileName(n.AsSpan())));
			}
			catch (Exception ex)
			{
				// TODO: Log
				return ParseKeysSlow(directoryNames);
			}
		}

		private TKey[] ParseKeysSlow(string[] directoryNames)
		{
			var keys = new TKey[directoryNames.Length];
			int count = 0;
			foreach (var directoryName in directoryNames)
			{
				if (_nameSerializer.TryParse(Path.GetFileName(directoryName.AsSpan()), out TKey? key))
				{
					keys[count++] = key;
				}
			}
			return count == keys.Length ? keys : keys[..count];
		}

		public ValueTask<ConfigurationResult<TValue>> ReadValueAsync<TValue>(TKey key, CancellationToken cancellationToken)
			=> _configurationService.ReadValueAsync<TValue>(_directory, _nameSerializer.ToString(key), cancellationToken);

		public ValueTask WriteValueAsync<TValue>(TKey key, TValue value, CancellationToken cancellationToken)
			where TValue : notnull
			=> _configurationService.WriteValueAsync(_directory, _nameSerializer.ToString(key), value, cancellationToken);

		public ValueTask DeleteValueAsync<TValue>(TKey key)
			=> _configurationService.DeleteValueAsync<TValue>(_directory, _nameSerializer.ToString(key));

		public ValueTask DeleteValuesAsync(TKey key)
			=> _configurationService.DeleteValuesAsync(_directory, _nameSerializer.ToString(key));

		public IConfigurationContainer GetContainer(TKey key)
			=> new ConfigurationContainer(_configurationService, PrepareChildDirectory(_directory, _nameSerializer.ToString(key), true));
	}

	private readonly AsyncLock _lock;
	private readonly string _directory;

	public ConfigurationService(string directory)
	{
		directory = Path.GetFullPath(directory);
		Directory.CreateDirectory(directory);
		_lock = new();
		_directory = directory;
	}

	private static string PrepareChildDirectory(string baseDirectory, string directoryName, bool allowGuid)
	{
		ArgumentNullException.ThrowIfNull(directoryName);
		// This first check is a relatively quick way to validate that the directory doesn't contain any directory separators.
		// It does however not prevent the name from being ".", ".." and similar.
		if (Path.GetFileName(directoryName.AsSpan()).Length == directoryName.Length && (allowGuid || !Guid.TryParseExact(directoryName, "D", out _)))
		{
			// We only need to compute the full path in order to validate that the requested directory is correctly rooted under the parent directory.
			string childDirectory = Path.GetFullPath(Path.Combine(baseDirectory, directoryName));
			if (childDirectory.Length > baseDirectory.Length && childDirectory.StartsWith(baseDirectory))
			{
				Directory.CreateDirectory(childDirectory);
				return childDirectory;
			}
		}
		throw new InvalidOperationException($"Invalid directory name: {directoryName}.");
	}

	public IConfigurationContainer GetContainer(string containerName)
		=> new ConfigurationContainer(this, PrepareChildDirectory(_directory, containerName, true));

	public IConfigurationContainer<TKey> GetContainer<TKey>(string containerName, INameSerializer<TKey> nameSerializer)
		=> new ConfigurationContainer<TKey>(this, PrepareChildDirectory(_directory, containerName, true), nameSerializer);

	private async ValueTask<ConfigurationResult<T>> ReadValueAsync<T>(string directory, string? key, CancellationToken cancellationToken)
	{
		string typeId = TypeId.GetString<T>();

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			string configurationDirectory = directory;

			if (key is not null)
			{
				configurationDirectory = Path.Combine(directory, key);

				if (!Directory.Exists(configurationDirectory))
				{
					return new(ConfigurationStatus.MissingContainer);
				}
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

	private async ValueTask WriteValueAsync<T>(string directory, string? key, T value, CancellationToken cancellationToken)
	{
		string typeId = TypeId.GetString<T>();
		if (value is null) throw new ArgumentOutOfRangeException(nameof(value));

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			string configurationDirectory = directory;

			if (key is not null)
			{
				configurationDirectory = Path.Combine(directory, key);
				Directory.CreateDirectory(configurationDirectory);
			}

			string fileName = Path.Combine(configurationDirectory, typeId) + ".json";

			using var file = File.Create(fileName);
			await JsonSerializer.SerializeAsync(file, value, JsonSerializerOptions, cancellationToken).ConfigureAwait(false);
		}
	}

	private async ValueTask DeleteValuesAsync(string directory, string key)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			Directory.Delete(Path.Combine(directory, key), true);
		}
	}

	private async ValueTask DeleteValueAsync<T>(string directory, string key)
	{
		string typeId = TypeId.GetString<T>();

		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			File.Delete(Path.Combine(directory, key, typeId) + ".json");
		}
	}

	private async ValueTask DeleteValueAsync<T>(string directory)
	{
		string typeId = TypeId.GetString<T>();

		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			File.Delete(Path.Combine(directory, typeId) + ".json");
		}
	}

	public async ValueTask<string[]> GetDirectoryNamesAsync(string directory, string pattern, CancellationToken cancellationToken)
	{
		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			return Directory.GetDirectories(directory, pattern, SearchOption.TopDirectoryOnly);
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
