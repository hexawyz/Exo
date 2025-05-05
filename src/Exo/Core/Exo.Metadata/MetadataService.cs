using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Exo.Metadata;

public abstract class MetadataResolver : IDisposable
{
	private ExoArchive? _overrideArchive;
	private ExoArchive? _mainArchive;
	private ExoArchive[]? _additionalArchives;
	private readonly Dictionary<string, ExoArchive> _additionalArchivesByName;
	private readonly Lock _lock;

	public MetadataResolver()
	{
		_additionalArchives = [];
		_additionalArchivesByName = new();
		_lock = new();
	}

	public MetadataResolver(string mainArchiveFileName)
	{
		ArgumentNullException.ThrowIfNull(mainArchiveFileName);

		_mainArchive = new(mainArchiveFileName);
		_additionalArchives = [];
		_additionalArchivesByName = new();
		_lock = new();
	}

	public void Dispose()
	{
		lock (_lock)
		{
			if (_mainArchive is not null)
			{
				_mainArchive.Dispose();
				_mainArchive = null;
			}
			if (_overrideArchive is not null)
			{
				_overrideArchive.Dispose();
				_overrideArchive = null;
			}
			if (_additionalArchives is not null)
			{
				foreach (var archive in _additionalArchives)
				{
					archive.Dispose();
				}
				_additionalArchives = null;
			}
			_additionalArchivesByName.Clear();
		}
	}

	public void AddArchive(string fileName)
	{
		lock (_lock)
		{
			if (_additionalArchives is null) return;
			if (_additionalArchivesByName.ContainsKey(fileName)) return;
			_additionalArchivesByName.Add(fileName, new(fileName));

			var newArchives = new ExoArchive[_additionalArchives.Length + 1];
			Array.Copy(_additionalArchives, 0, newArchives, 0, _additionalArchives.Length);
			newArchives[^1] = new ExoArchive(fileName);
			_additionalArchives = newArchives;
		}
	}

	public void RemoveArchive(string fileName)
	{
		lock (_lock)
		{
			if (!_additionalArchivesByName.Remove(fileName, out var archive)) return;
			if (_additionalArchives is not null && Array.IndexOf(_additionalArchives, archive) is int index and >= 0)
			{
				if (_additionalArchives.Length == 1)
				{
					_additionalArchives = [];
				}
				else
				{
					var newArchives = new ExoArchive[_additionalArchives.Length - 1];
					Array.Copy(_additionalArchives, 0, newArchives, 0, index);
					Array.Copy(_additionalArchives, index + 1, newArchives, index, newArchives.Length - index);
					_additionalArchives = newArchives;
				}
			}
			archive.Dispose();
		}
	}

	public void SetOverrideArchive(string? fileName)
	{
		lock (_lock)
		{
			if (_overrideArchive is not null)
			{
				_overrideArchive.Dispose();
				_overrideArchive = null;
			}
			if (fileName is not null)
			{
				_overrideArchive = new(fileName);
			}
		}
	}

	protected bool FindFile(ReadOnlySpan<byte> key, out ExoArchiveFile file)
	{
		lock (_lock)
		{
			if (_overrideArchive is not null && _overrideArchive.TryGetFileEntry(key, out file) || _mainArchive is not null && _mainArchive.TryGetFileEntry(key, out file))
			{
				return true;
			}

			if (_additionalArchives is not null)
			{
				foreach (var archive in _additionalArchives)
				{
					if (archive.TryGetFileEntry(key, out file)) return true;
				}
			}
		}

		file = default;
		return false;
	}
}

public sealed class StringMetadataResolver : MetadataResolver
{
	public StringMetadataResolver(string mainArchiveFileName) : base(mainArchiveFileName)
	{
	}

	public string? GetStringAsync(CultureInfo? culture, Guid stringId)
	{
		// For strings, we will use a key of the form "strings/<ID>/<Culture>".
		// Culture Names returned by CultureInfo.Name should be at most 11 characters, often less.
		// We will try to check, in order:
		// 1. The culture that was requested.
		// 2. The parent culture of the requested culture.
		// 3. The "en" culture, if not already checked.
		// For now, it is simpler to consider English to be the main fallback culture, but there are some cases where we might want to have true "culture-less" strings.
		// In that case, we might consider having strings keyed without a culture name, but need to think about how this would work first:
		// - In the JSON we'll easily have something like { "en": "Welcome", "fr": Bienvenue" }, so how would this work without a culture ? Maybe a string instead of an object ?
		// - What would be the lookup priority in that case ? Should the culture-less string be searched first or not ? (Depends if the no-culture string is in addition to the others or exclusive)
		// - Should there be an explicit control over what we should return here? And how would we chose?

		Span<byte> key = stackalloc byte[16 + 12];
		stringId.TryWriteBytes(key);
		key[16] = (byte)'/';

		var currentCulture = culture;
		ExoArchiveFile file;
		while (currentCulture?.Name is { Length: > 0 } and not "en")
		{
			int count = Encoding.UTF8.GetBytes(currentCulture.Name, key[17..]);
			if (FindFile(key[..(17 + count)], out file))
			{
				return Encoding.UTF8.GetString(file.DangerousGetSpan());
			}
			currentCulture = currentCulture.Parent;
		}

		"en"u8.CopyTo(key[17..]);
		if (FindFile(key[..19], out file))
		{
			return Encoding.UTF8.GetString(file.DangerousGetSpan());
		}

		return null;
	}
}

public sealed class DeviceMetadataResolver<T> : MetadataResolver
	where T : struct, IExoMetadata
{
	public DeviceMetadataResolver() : base()
	{
	}

	public DeviceMetadataResolver(string mainArchiveFileName) : base(mainArchiveFileName)
	{
	}

	private bool FindFile(string driverKey, string compatibleId, Guid itemId, out ExoArchiveFile file)
	{
		Span<byte> key = stackalloc byte[17 + Encoding.UTF8.GetByteCount(driverKey) + 1 + Encoding.UTF8.GetByteCount(compatibleId)];

		// First try to locate the most specific data for a key, then move out to the less specific version.

		itemId.TryWriteBytes(key);
		key[16] = (byte)'/';
		int keyLength = 17;
		int driverKeyLength = Encoding.UTF8.GetBytes(driverKey, key[keyLength..]);
		keyLength += driverKeyLength;
		key[keyLength] = (byte)'/';
		keyLength++;
		int compatibleIdLength = Encoding.UTF8.GetBytes(compatibleId, key[keyLength..]);
		keyLength += compatibleIdLength;

		if (FindFile(key[..keyLength], out file)) return true;
		keyLength = keyLength - compatibleIdLength - 1;
		if (FindFile(key[..keyLength], out file)) return true;
		keyLength = keyLength - driverKeyLength - 1;
		return FindFile(key[..keyLength], out file);
	}

	public bool TryGetData(string driverKey, string compatibleId, Guid itemId, out T value)
	{
		if (FindFile(driverKey, compatibleId, itemId, out var file))
		{
			value = MetadataSerializer.Deserialize<T>(file.DangerousGetSpan());
			return true;
		}
		value = default;
		return false;
	}
}

public sealed class MetadataService : IMetadataService, IDisposable
{
	private readonly StringMetadataResolver _stringMetadataResolver;
	private readonly DeviceMetadataResolver<LightingEffectMetadata> _lightingEffectMetadataResolver;
	private readonly DeviceMetadataResolver<LightingZoneMetadata> _lightingZoneMetadataResolver;
	private readonly DeviceMetadataResolver<SensorMetadata> _sensorMetadataResolver;
	private readonly DeviceMetadataResolver<CoolerMetadata> _coolerMetadataResolver;

	public MetadataService
	(
		StringMetadataResolver stringMetadataResolver,
		DeviceMetadataResolver<LightingEffectMetadata> lightingEffectMetadataResolver,
		DeviceMetadataResolver<LightingZoneMetadata> lightingZoneMetadataResolver,
		DeviceMetadataResolver<SensorMetadata> sensorMetadataResolver,
		DeviceMetadataResolver<CoolerMetadata> coolerMetadataResolver
	)
	{
		_stringMetadataResolver = stringMetadataResolver;
		_lightingEffectMetadataResolver = lightingEffectMetadataResolver;
		_lightingZoneMetadataResolver = lightingZoneMetadataResolver;
		_sensorMetadataResolver = sensorMetadataResolver;
		_coolerMetadataResolver = coolerMetadataResolver;
	}

	public void Dispose()
	{
		_stringMetadataResolver.Dispose();
		_lightingEffectMetadataResolver.Dispose();
		_lightingZoneMetadataResolver.Dispose();
		_sensorMetadataResolver.Dispose();
		_coolerMetadataResolver.Dispose();
	}

	public string? GetString(CultureInfo? culture, Guid stringId)
		=> _stringMetadataResolver.GetStringAsync(culture, stringId);

	public bool TryGetLightingEffectMetadata(string driverKey, string compatibleId, Guid lightingEffectId, out LightingEffectMetadata value)
		=> _lightingEffectMetadataResolver.TryGetData(driverKey, compatibleId, lightingEffectId, out value);

	public bool TryGetLightingZoneMetadata(string driverKey, string compatibleId, Guid lightingZoneId, out LightingZoneMetadata value)
		=> _lightingZoneMetadataResolver.TryGetData(driverKey, compatibleId, lightingZoneId, out value);

	public bool TryGetSensorMetadata(string driverKey, string compatibleId, Guid sensorId, out SensorMetadata value)
		=> _sensorMetadataResolver.TryGetData(driverKey, compatibleId, sensorId, out value);

	public bool TryGetCoolerMetadata(string driverKey, string compatibleId, Guid coolerId, out CoolerMetadata value)
		=> _coolerMetadataResolver.TryGetData(driverKey, compatibleId, coolerId, out value);
}

/// <summary>The serializer used for metadata types.</summary>
/// <remarks>This uses custom serialization for every type, but the interface is abstracted to be generic in order to simplify use.</remarks>
public static class MetadataSerializer
{
	public static byte[] Serialize<T>(T value)
		where T : struct, IExoMetadata
	{
		if (typeof(T) == typeof(LightingEffectMetadata)) return Serialize(in Unsafe.As<T, LightingEffectMetadata>(ref value));
		if (typeof(T) == typeof(LightingZoneMetadata)) return Serialize(in Unsafe.As<T, LightingZoneMetadata>(ref value));
		if (typeof(T) == typeof(CoolerMetadata)) return Serialize(in Unsafe.As<T, CoolerMetadata>(ref value));
		if (typeof(T) == typeof(SensorMetadata)) return Serialize(in Unsafe.As<T, SensorMetadata>(ref value));
		throw new InvalidOperationException();
	}

	private static byte[] Serialize(in LightingEffectMetadata value)
	{
		var array = new byte[20];
		value.NameStringId.TryWriteBytes(array);
		LittleEndian.Write(ref array[16], value.DisplayOrder);
		return array;
	}

	private static byte[] Serialize(in LightingZoneMetadata value)
	{
		var array = new byte[22];
		value.NameStringId.TryWriteBytes(array);
		LittleEndian.Write(ref array[16], (uint)value.DisplayOrder);
		array[20] = (byte)value.ComponentType;
		array[21] = (byte)value.Shape;
		return array;
	}

	private static byte[] Serialize(in CoolerMetadata value)
	{
		var array = new byte[16];
		value.NameStringId.TryWriteBytes(array);
		return array;
	}

	private static byte[] Serialize(in SensorMetadata value)
	{
		var array = new byte[48 + (value.PresetControlCurveSteps is not null ? value.PresetControlCurveSteps.Length << 3 : 0)];
		Serialize(in value, array);
		return array;
	}

	private static void Serialize(in SensorMetadata value, Span<byte> buffer)
	{
		value.NameStringId.TryWriteBytes(buffer);
		buffer[16] = (byte)value.Category;
		LittleEndian.Write(ref buffer[24], value.MinimumValue);
		LittleEndian.Write(ref buffer[32], value.MaximumValue);
		WriteDoubleArray(buffer[40..], value.PresetControlCurveSteps);
	}

	public static T Deserialize<T>(ReadOnlySpan<byte> data)
		where T : struct, IExoMetadata
	{
		if (typeof(T) == typeof(LightingEffectMetadata)) return Unsafe.BitCast<LightingEffectMetadata, T>(DeserializeEffectMetadata(data));
		if (typeof(T) == typeof(LightingZoneMetadata)) return Unsafe.BitCast<LightingZoneMetadata, T>(DeserializeLightingZoneMetadata(data));
		if (typeof(T) == typeof(CoolerMetadata)) return Unsafe.BitCast<CoolerMetadata, T>(DeserializeCoolerMetadata(data));
		if (typeof(T) == typeof(SensorMetadata)) return Unsafe.BitCast<SensorMetadata, T>(DeserializeSensorMetadata(data));
		throw new InvalidOperationException();
	}

	private static LightingEffectMetadata DeserializeEffectMetadata(ReadOnlySpan<byte> data)
		=> new LightingEffectMetadata
		{
			NameStringId = new Guid(data[..16]),
			DisplayOrder = MemoryMarshal.Read<ushort>(data[16..]),
		};

	private static LightingZoneMetadata DeserializeLightingZoneMetadata(ReadOnlySpan<byte> data)
		=> new LightingZoneMetadata
		{
			NameStringId = new Guid(data[..16]),
			DisplayOrder = (int)LittleEndian.ReadUInt32(in data[16])
		};

	private static CoolerMetadata DeserializeCoolerMetadata(ReadOnlySpan<byte> data)
		=> new CoolerMetadata
		{
			NameStringId = new Guid(data[..16]),
		};

	private static SensorMetadata DeserializeSensorMetadata(ReadOnlySpan<byte> data)
		=> new SensorMetadata
		{
			NameStringId = new Guid(data[..16]),
			Category = (SensorCategory)data[16],
			MinimumValue = LittleEndian.ReadDouble(in data[24]),
			MaximumValue = LittleEndian.ReadDouble(in data[32]),
			PresetControlCurveSteps = ReadDoubleArray(data[40..]),
		};

	private static double[]? ReadDoubleArray(ReadOnlySpan<byte> data)
	{
		uint count = LittleEndian.ReadUInt32(in MemoryMarshal.GetReference(data));
		if (count == 0) return null;
		data = data[8..];
		if (data.Length >>> 3 < count) throw new InvalidDataException("There are not enough bytes for the specified count.");
		var array = new double[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = LittleEndian.ReadDouble(in MemoryMarshal.GetReference(data));
			data = data[8..];
		}
		return array;
	}

	private static int WriteDoubleArray(Span<byte> buffer, double[]? array)
	{
		LittleEndian.Write(ref MemoryMarshal.GetReference(buffer), array is null ? 0 : (uint)array.Length);
		int length = 8;
		if (array is not null)
		{
			for (int i = 0; i < array.Length; i++)
			{
				LittleEndian.Write(ref buffer[length], array[i]);
				length += 8;
			}
		}
		return length;
	}
}

/// <summary>Interface used internally to support metadata parsing.</summary>
/// <remarks>
/// The sole purpose of this interface is to provide better generic constraints for metadata-related classes.
/// The goal is to avoid code errors and catch them at build time.
/// It is public because it has to, but you should not implement this interface in any custom type.
/// </remarks>
public interface IExoMetadata
{
}

public readonly struct LightingEffectMetadata : IExoMetadata
{
	public required Guid NameStringId { get; init; }
	public required uint DisplayOrder { get; init; }
}

public readonly struct LightingZoneMetadata : IExoMetadata
{
	public required Guid NameStringId { get; init; }
	public required int DisplayOrder { get; init; }
	public LightingZoneComponentType ComponentType { get; init; }
	public LightingZoneShape Shape { get; init; }
}

public readonly struct CoolerMetadata : IExoMetadata
{
	public required Guid NameStringId { get; init; }
}

/// <summary>Represents sensor metadata.</summary>
public readonly struct SensorMetadata : IExoMetadata
{
	/// <summary>Gets a string ID that indicates the name of the sensor.</summary>
	public required Guid NameStringId { get; init; }
	/// <summary>Gets the sensor category.</summary>
	public required SensorCategory Category { get; init; }
	/// <summary>Gets the minimum value that can be reached by the sensor.</summary>
	public required double MinimumValue { get; init; }
	/// <summary>Gets the maximum value that can be reached by the sensor.</summary>
	public required double MaximumValue { get; init; }
	/// <summary>Gets preset control curve steps to use for using this sensor as the input of a control curve.</summary>
	/// <remarks>
	/// If <see langword="null"/>, this will indicate that the sensor should not be used as a control curve input.
	/// While in general, we don't want to forbid anything, there really are some sensors for which it doesn't make any sense to have 
	/// </remarks>
	public double[]? PresetControlCurveSteps { get; init; }
}

public enum SensorCategory : byte
{
	Other = 0,
	Load = 1,
	Memory = 2,
	Battery = 3,
	Fan = 4,
	Pump = 5,
	Temperature = 6,
	RotationSpeed = 7,
	LinearSpeed = 8,
	Power = 9,
	Voltage = 10,
	Current = 11,
	Frequency = 12,
}

/// <summary>Indicates the type of component that is associated with this lighting zone.</summary>
/// <remarks>This information will be used by the UI to show a better icon.</remarks>
public enum LightingZoneComponentType : byte
{
	Unknown = 0,
	Indicator = 1,
	Logo = 2,
	Strip = 3,
	Fan = 4,
	Pump = 5,
	MemoryStick = 6,
	Button = 7,
}

/// <summary>Indicates the general shape of a lighting zone.</summary>
/// <remarks>This will allow the UI to provide a better presentation for the lighting zone when necessary.</remarks>
public enum LightingZoneShape : byte
{
	Other = 0,
	Dot,
	Line,
	Square,
	Rectangle,
	Ring,
	Disc,
	Text,
	Chevron,
}
