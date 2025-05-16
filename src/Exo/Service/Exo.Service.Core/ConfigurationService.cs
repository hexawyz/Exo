using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Exo.Configuration;

namespace Exo.Service;

public sealed class ConfigurationService : IConfigurationNode
{
	private static bool ShouldSerializeImmutableArray<T>(object ignore, object parent, object? value)
		=> value is not null && !Unsafe.Unbox<ImmutableArray<T>>(value).IsDefaultOrEmpty;

	// Options will become read-only after first use, so we can just share it as is, trusting that no other piece of code will try to change this.
	internal static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
	{
		AllowTrailingCommas = false,
		WriteIndented = false,
		NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter() },
		TypeInfoResolver = new DefaultJsonTypeInfoResolver
		{
			Modifiers =
			{
				ti =>
				{
					foreach (var property in ti.Properties)
					{
						if (typeof(System.Collections.ICollection).IsAssignableFrom(property.PropertyType))
						{
							if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
							{
								property.ShouldSerialize = typeof(ConfigurationService)
									.GetMethod(nameof(ShouldSerializeImmutableArray), BindingFlags.Static | BindingFlags.NonPublic)!
									.MakeGenericMethod(property.PropertyType.GetGenericArguments()).CreateDelegate<Func<object, object?, bool>>(null);
								property.ObjectCreationHandling = JsonObjectCreationHandling.Replace;
								property.IsRequired = false;
								continue;
							}
							else
							{
								property.ShouldSerialize = (_, obj) => obj is not null && ((System.Collections.ICollection)obj).Count != 0;
							}
						}
						if (property.IsRequired)
						{
							property.ShouldSerialize = (_, _) => true;
						}
					}
				}
			}
		},
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

		public ValueTask<ConfigurationResult<TValue>> ReadValueAsync<TValue>(JsonTypeInfo<TValue> jsonTypeInfo, CancellationToken cancellationToken)
			=> _configurationService.ReadValueAsync<TValue>(_directory, null, jsonTypeInfo, cancellationToken);

		public ValueTask WriteValueAsync<TValue>(TValue value, JsonTypeInfo<TValue> jsonTypeInfo, CancellationToken cancellationToken)
			where TValue : notnull
			=> _configurationService.WriteValueAsync(_directory, null, value, jsonTypeInfo, cancellationToken);

		public ValueTask DeleteValueAsync<TValue>()
			=> _configurationService.DeleteValueAsync<TValue>(_directory);

		public ValueTask DeleteAllValuesAsync()
			=> _configurationService.DeleteValuesAsync(_directory);

		public IConfigurationContainer GetContainer(string containerName)
			=> new ConfigurationContainer(_configurationService, PrepareChildDirectory(_directory, containerName, false, true)!);

		public IConfigurationContainer<TKey> GetContainer<TKey>(string containerName, INameSerializer<TKey> nameSerializer)
			=> new ConfigurationContainer<TKey>(_configurationService, PrepareChildDirectory(_directory, containerName, false, true)!, nameSerializer);

		public IConfigurationContainer? TryGetContainer(string containerName)
			=> PrepareChildDirectory(_directory, containerName, false, false) is { } childDirectory ?
				new ConfigurationContainer(_configurationService, childDirectory) :
				null;

		public IConfigurationContainer<TKey>? TryGetContainer<TKey>(string containerName, INameSerializer<TKey> nameSerializer)
			=> PrepareChildDirectory(_directory, containerName, false, false) is { } childDirectory ?
				new ConfigurationContainer<TKey>(_configurationService, childDirectory, nameSerializer) :
				null;
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

		public ValueTask<ConfigurationResult<TValue>> ReadValueAsync<TValue>(TKey key, JsonTypeInfo<TValue> jsonTypeInfo, CancellationToken cancellationToken)
			=> _configurationService.ReadValueAsync<TValue>(_directory, _nameSerializer.ToString(key), jsonTypeInfo, cancellationToken);

		public ValueTask WriteValueAsync<TValue>(TKey key, TValue value, JsonTypeInfo<TValue> jsonTypeInfo, CancellationToken cancellationToken)
			where TValue : notnull
			=> _configurationService.WriteValueAsync(_directory, _nameSerializer.ToString(key), value, jsonTypeInfo, cancellationToken);

		public ValueTask DeleteValueAsync<TValue>(TKey key)
			=> _configurationService.DeleteValueAsync<TValue>(_directory, _nameSerializer.ToString(key));

		public ValueTask DeleteValuesAsync(TKey key)
			=> _configurationService.DeleteValuesAsync(_directory, _nameSerializer.ToString(key));

		public IConfigurationContainer GetContainer(TKey key)
			=> new ConfigurationContainer(_configurationService, PrepareChildDirectory(_directory, _nameSerializer.ToString(key), true, true)!);

		public ValueTask DeleteAllContainersAsync() => _configurationService.DeleteContainersAsync(_directory, _nameSerializer);
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

	private static string? PrepareChildDirectory(string baseDirectory, string directoryName, bool allowGuid, bool createIfNotExists)
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
				if (createIfNotExists)
				{
					Directory.CreateDirectory(childDirectory);
				}
				else if (!Directory.Exists(childDirectory))
				{
					return null;
				}
				return childDirectory;
			}
		}
		throw new InvalidOperationException($"Invalid directory name: {directoryName}.");
	}

	public IConfigurationContainer GetContainer(string containerName)
		=> new ConfigurationContainer(this, PrepareChildDirectory(_directory, containerName, true, true)!);

	public IConfigurationContainer<TKey> GetContainer<TKey>(string containerName, INameSerializer<TKey> nameSerializer)
		=> new ConfigurationContainer<TKey>(this, PrepareChildDirectory(_directory, containerName, true, true)!, nameSerializer);

	public IConfigurationContainer? TryGetContainer(string containerName)
		=> PrepareChildDirectory(_directory, containerName, true, false) is { } childDirectory ?
			new ConfigurationContainer(this, childDirectory) :
			null;

	public IConfigurationContainer<TKey>? TryGetContainer<TKey>(string containerName, INameSerializer<TKey> nameSerializer)
		=> PrepareChildDirectory(_directory, containerName, true, false) is { } childDirectory ?
			new ConfigurationContainer<TKey>(this, childDirectory, nameSerializer) :
			null;

	public IConfigurationContainer GetRootContainer()
		=> new ConfigurationContainer(this, _directory);

	private async ValueTask<ConfigurationResult<T>> ReadValueAsync<T>(string directory, string? key, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
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
				try
				{
					var result = await JsonSerializer.DeserializeAsync<T>(file, jsonTypeInfo, cancellationToken).ConfigureAwait(false);

					return result is null ? new(ConfigurationStatus.InvalidValue) : new(result);
				}
				catch
				{
					return new(ConfigurationStatus.MalformedData);
				}
			}
			return new(ConfigurationStatus.MissingValue);
		}
	}

	private async ValueTask WriteValueAsync<T>(string directory, string? key, T value, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
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
			await JsonSerializer.SerializeAsync(file, value, jsonTypeInfo, cancellationToken).ConfigureAwait(false);
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

	private async ValueTask DeleteValuesAsync(string directory)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			string[] fileNames = Directory.GetFiles(directory, "????????-????-????-????-????????????.json", SearchOption.TopDirectoryOnly);

			foreach (string fileName in fileNames)
			{
				if (GuidNameSerializer.Instance.TryParse(Path.GetFileNameWithoutExtension(fileName), out _))
				{
					File.Delete(fileName);
				}
			}
		}
	}

	private async ValueTask DeleteContainersAsync<TKey>(string directory, INameSerializer<TKey> nameSerializer)
	{
		using (await _lock.WaitAsync(default).ConfigureAwait(false))
		{
			string[] directoryNames = Directory.GetDirectories(directory, nameSerializer.FileNamePattern, SearchOption.TopDirectoryOnly);

			foreach (string directoryName in directoryNames)
			{
				if (nameSerializer.TryParse(Path.GetFileName(directoryName), out _))
				{
					Directory.Delete(directoryName, true);
				}
			}
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
	public Task BackupAsync(Stream stream) => Task.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new NotImplementedException()));
}
