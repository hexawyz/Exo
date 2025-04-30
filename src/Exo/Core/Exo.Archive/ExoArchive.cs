using System.Collections.Immutable;
using System.IO.Hashing;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Exo.Metadata;

/// <summary>Accesses a data archive in the Exo archive format.</summary>
/// <remarks>
/// This format is a serialized hash table that can be accessed by random accesses as a memory mapped file.
/// It is, in concept, inspired from the MPQ format. It has, however, no special features such as compression, signature or encryption.
/// The goal of this format is to be quickly accessible without the app needing to load a bunch of data in its own heap.
/// Obviously, having the file accessed as a memory map will still allocate virtual memory within the process, but the OS is free to free the physical memory pages at any time.
/// </remarks>
public sealed unsafe class ExoArchive : IDisposable
{
	static ExoArchive()
	{
		if (!BitConverter.IsLittleEndian) throw new InvalidOperationException("Big-endian architectures are not supported.");
	}

	private IDisposable? _file;
	private MemoryMappedViewAccessor? _memoryMappedViewAccessor;
	private volatile byte* _pointer;
	private readonly uint _entryCount;
	private readonly uint _blockSize;
	private readonly uint _dataOffset;
	private readonly ulong _length;

	public ExoArchive(string fileName)
	{
		var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
		try
		{
			_length = (ulong)stream.Length;
			var memoryMappedFile = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
			_memoryMappedViewAccessor = memoryMappedFile.CreateViewAccessor(0, stream.Length, MemoryMappedFileAccess.Read);
			_file = memoryMappedFile;
			byte* pointer = null;
			_memoryMappedViewAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
			if (pointer == null) throw new InvalidOperationException();
			_pointer = pointer;

			ParseHeader(_pointer, _length, out _entryCount, out _blockSize, out _dataOffset);
		}
		catch
		{
			stream.Dispose();
			throw;
		}
	}

	public ExoArchive(UnmanagedMemoryStream stream) : this(stream, false) { }

	public ExoArchive(UnmanagedMemoryStream stream, bool leaveOpen)
	{
		ArgumentNullException.ThrowIfNull(stream);
		try
		{
			_length = (ulong)stream.Length;
			if (!leaveOpen) _file = stream;
			_pointer = stream.PositionPointer;

			ParseHeader(_pointer, _length, out _entryCount, out _blockSize, out _dataOffset);
		}
		catch
		{
			if (!leaveOpen) stream.Dispose();
			throw;
		}
	}

	private static void ParseHeader(byte* pointer, ulong length, out uint entryCount, out uint blockSize, out uint dataOffset)
	{
		if (length < (ulong)Unsafe.SizeOf<ExoArchiveHeader>()) throw new InvalidDataException("File is too small.");
		ref var header = ref Unsafe.AsRef<ExoArchiveHeader>(pointer);
		if (header.Signature != 0x52414F58) throw new InvalidDataException("Invalid signature.");
		if (header.Version != 1) throw new InvalidDataException("Invalid version.");
		if (header.HashTableLength != 0 && ((int)header.HashTableLength < 0 || !BitOperations.IsPow2(header.HashTableLength))) throw new InvalidDataException("Invalid hash table length.");
		if (header.BlockSize < 8 || (header.BlockSize & 0x7) != 0) throw new InvalidDataException("Invalid block size.");
		entryCount = header.HashTableLength;
		blockSize = header.BlockSize;
		if (checked((ulong)Math.BigMul(unchecked((int)entryCount), Unsafe.SizeOf<ExoArchiveHashTableEntry>())) > length) throw new InvalidDataException("Invalid file size.");
		// Adjust the data offset to be a multiple of the block size.
		uint offset = checked(unchecked((uint)Unsafe.SizeOf<ExoArchiveHeader>()) + (uint)Math.BigMul(unchecked((int)entryCount), unchecked((int)Unsafe.SizeOf<ExoArchiveHashTableEntry>())));
		if (offset % blockSize is > 0 and uint r) offset += blockSize - r;
		dataOffset = offset;
	}

	public void Dispose()
	{
		_pointer = null;
		if (Interlocked.Exchange(ref _memoryMappedViewAccessor, null) is { } accessor)
		{
			accessor.SafeMemoryMappedViewHandle.DangerousRelease();
			accessor.Dispose();
		}
		Interlocked.Exchange(ref _file, null)?.Dispose();
	}

	private ref byte DataReference
	{
		get
		{
			var pointer = _pointer;

			if (pointer is null) throw new ObjectDisposedException(GetType().FullName);

			return ref Unsafe.AsRef<byte>(_pointer);
		}
	}

	private ref ExoArchiveHashTableEntry GetEntry(uint entryIndex)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(entryIndex, _entryCount);

		return ref GetEntry(ref DataReference, entryIndex);
	}

	static private ref ExoArchiveHashTableEntry GetEntry(ref byte dataReference, uint entryIndex)
		=> ref Unsafe.Add(ref Unsafe.As<byte, ExoArchiveHashTableEntry>(ref Unsafe.AddByteOffset(ref dataReference, (nuint)Unsafe.SizeOf<ExoArchiveHeader>())), entryIndex);

	internal uint GetFileLength(uint entryIndex) => GetEntry(entryIndex).FileLength;

	internal ReadOnlySpan<byte> DangerousGetSpan(uint entryIndex)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(entryIndex, _entryCount);

		ref var dataReference = ref DataReference;
		ref var entry = ref GetEntry(ref dataReference, entryIndex);

		nuint blockOffset = checked(_dataOffset + (nuint)Math.BigMul(unchecked((int)entry.BlockIndex), unchecked((int)_blockSize)));

		return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AddByteOffset(ref dataReference, blockOffset), checked((int)entry.FileLength));
	}

	public bool TryGetFileEntry(ReadOnlySpan<byte> key, out ExoArchiveFile file)
	{
		if (_entryCount == 0) goto NotFound;

		ulong hash1 = XxHash3.HashToUInt64(key, 0x5649484352414F58);
		ulong hash2 = XxHash3.HashToUInt64(key, 0);

		ref var firstEntry = ref Unsafe.As<byte, ExoArchiveHashTableEntry>(ref Unsafe.AddByteOffset(ref DataReference, Unsafe.SizeOf<ExoArchiveHeader>()));

		int referenceEntryIndex = (int)(hash1 & (_entryCount - 1));
		int i = referenceEntryIndex;

		while (true)
		{
			ref var entry = ref Unsafe.Add(ref firstEntry, i);
			if (entry.Hash == hash2)
			{
				ValidateEntry(ref entry);
				file = new(this, (uint)i);
				return true;
			}
			if (entry.BlockIndex + 1 == 0) break;
			i++;
			if (i == _entryCount) i = 0;
			if (i == referenceEntryIndex) break;
		}

	NotFound:;
		file = default;
		return false;
	}

	private void ValidateEntry(ref ExoArchiveHashTableEntry entry)
	{
		nuint blockOffset = checked(_dataOffset + (nuint)Math.BigMul(unchecked((int)entry.BlockIndex), unchecked((int)_blockSize)));
		if (blockOffset > _length || checked(blockOffset + entry.FileLength) > _length) throw new InvalidOperationException("Invalid file entry.");
	}
}

public readonly struct ExoArchiveFile
{
	private readonly ExoArchive _archive;
	private readonly uint _entryIndex;

	internal ExoArchiveFile(ExoArchive archive, uint entryIndex) : this()
	{
		_archive = archive;
		_entryIndex = entryIndex;
	}

	public uint Length => _archive.GetFileLength(_entryIndex);

	/// <summary>Retrieves a read-only span that can be used to access the data.</summary>
	/// <remarks>It is the responsibility of the caller to make sure that the archive is not disposed while the data is being accessed.</remarks>
	/// <returns>A read-only span containing the file data.</returns>
	public ReadOnlySpan<byte> DangerousGetSpan() => _archive.DangerousGetSpan(_entryIndex);
}

internal readonly struct ExoArchiveHeader
{
	// "XOAR"
	public readonly uint Signature;
	// 1
	public readonly uint Version;
	// Strictly positive number indicating the length of the hash table used for mapping filenames to data entries.
	public readonly uint HashTableLength;
	// Block size of the archive. Also data alignment. Must be a multiple of a power of two ≥ 8.
	public readonly uint BlockSize;

	public ExoArchiveHeader(uint signature, uint version, uint hashTableLength, uint blockSize)
	{
		Signature = signature;
		Version = version;
		HashTableLength = hashTableLength;
		BlockSize = blockSize;
	}
}

internal readonly struct ExoArchiveHashTableEntry
{
	public static ExoArchiveHashTableEntry Empty = new(0, uint.MaxValue, 0);

	// Hash of the entry key, used for validation.
	public readonly ulong Hash;
	// Index of the first block of this file.
	public readonly uint BlockIndex;
	// Length of the file in bytes.
	public readonly uint FileLength;

	public ExoArchiveHashTableEntry(ulong hash, uint blockIndex, uint fileLength)
	{
		Hash = hash;
		BlockIndex = blockIndex;
		FileLength = fileLength;
	}
}

/// <summary>A simple builder for exo archives.</summary>
/// <remarks>
/// This simple builder will keep all the data in memory before writing the file.
/// Better implementations are possible, but this one should be enough considering the current use-case.
/// </remarks>
public sealed class InMemoryExoArchiveBuilder
{
	private readonly struct Key(ulong hash1, ulong hash2)
	{
		public readonly ulong Hash1 = hash1;
		public readonly ulong Hash2 = hash2;
	}

	private readonly struct File
	{
		public readonly ImmutableArray<Key> Keys;
		public readonly ImmutableArray<byte> Data;

		public File(ulong hash1, ulong hash2, ImmutableArray<byte> data)
		{
			Keys = [new(hash1, hash2)];
			Data = data;
		}

		public File(ImmutableArray<Key> keys, ImmutableArray<byte> data)
		{
			Keys = keys;
			Data = data;
		}
	}

	/// <summary>A reference to a file that was added.</summary>
	/// <remarks>This can be used to attach an extra name to the file.</remarks>
	public readonly struct FileReference
	{
		private readonly InMemoryExoArchiveBuilder _builder;
		private readonly nuint _index;

		internal FileReference(InMemoryExoArchiveBuilder builder, nuint index)
		{
			_builder = builder;
			_index = index;
		}

		internal nuint GetEntryIndex(InMemoryExoArchiveBuilder builder)
		{
			if (!ReferenceEquals(builder, _builder)) throw new InvalidOperationException();

			return _index;
		}
	}

	private const long ExoArchiveNameSeed1 = 0x5649484352414F58;

	private readonly HashSet<ulong> _hashes;
	private readonly List<File> _files;

	public InMemoryExoArchiveBuilder()
	{
		_hashes = new();
		_files = new();
	}

	private FileReference AddFile(File file)
	{
		int index = _files.Count;
		_files.Add(file);
		return new(this, (uint)index);
	}

	public FileReference AddFile(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
	{
		ulong hash1 = XxHash3.HashToUInt64(key, ExoArchiveNameSeed1);
		ulong hash2 = XxHash3.HashToUInt64(key, 0);

		if (!_hashes.Add(hash2)) throw new InvalidOperationException("An entry with the same hash has already been added.");

		return AddFile(new(hash1, hash2, data.ToImmutableArray()));
	}

	public FileReference AddFile(ReadOnlySpan<byte> key, ImmutableArray<byte> data)
	{
		ulong hash1 = XxHash3.HashToUInt64(key, ExoArchiveNameSeed1);
		ulong hash2 = XxHash3.HashToUInt64(key, 0);

		if (!_hashes.Add(hash2)) throw new InvalidOperationException("An entry with the same hash has already been added.");

		return AddFile(new(hash1, hash2, data));
	}

	public FileReference AddFile(ReadOnlySpan<byte> keys, ReadOnlySpan<int> keyLengths, ReadOnlySpan<byte> data)
	{
		var fileKeys = new Key[keyLengths.Length];
		int offset = 0;
		for (int i = 0; i < keyLengths.Length; i++)
		{
			int keyLength = keyLengths[i];
			var key = keys.Slice(offset, keyLength);
			offset += keyLength;

			ulong hash1 = XxHash3.HashToUInt64(key, ExoArchiveNameSeed1);
			ulong hash2 = XxHash3.HashToUInt64(key, 0);

			if (!_hashes.Add(hash2))
			{
				// Rollback registration of the previous keys.
				for (int j = 0; j < i; j++)
				{
					_hashes.Remove(fileKeys[j].Hash2);
				}
				throw new InvalidOperationException("An entry with the same hash has already been added.");
			}

			fileKeys[i] = new(hash1, hash2);
		}

		return AddFile(new(ImmutableCollectionsMarshal.AsImmutableArray(fileKeys), data.ToImmutableArray()));
	}

	public FileReference AddFile(ReadOnlySpan<byte> key, FileReference file)
	{
		ulong hash1 = XxHash3.HashToUInt64(key, ExoArchiveNameSeed1);
		ulong hash2 = XxHash3.HashToUInt64(key, 0);

		if (!_hashes.Add(hash2)) throw new InvalidOperationException("An entry with the same hash has already been added.");

		ref var entry = ref CollectionsMarshal.AsSpan(_files)[(int)file.GetEntryIndex(this)];
		entry = new([.. entry.Keys, new(hash1, hash2)], entry.Data);
		return file;
	}

	public FileReference AddFile(ReadOnlySpan<byte> keys, ReadOnlySpan<int> keyLengths, FileReference file)
	{
		ref var entry = ref CollectionsMarshal.AsSpan(_files)[(int)file.GetEntryIndex(this)];
		int previousKeyCount = entry.Keys.Length;
		var fileKeys = new Key[previousKeyCount + keyLengths.Length];
		entry.Keys.CopyTo(fileKeys);
		int offset = 0;
		for (int i = 0; i < keyLengths.Length; i++)
		{
			int keyLength = keyLengths[i];
			var key = keys.Slice(offset, keyLength);
			offset += keyLength;

			ulong hash1 = XxHash3.HashToUInt64(key, ExoArchiveNameSeed1);
			ulong hash2 = XxHash3.HashToUInt64(key, 0);

			if (!_hashes.Add(hash2))
			{
				// Rollback registration of the previous keys.
				for (int j = 0; j < i; j++)
				{
					_hashes.Remove(fileKeys[previousKeyCount + j].Hash2);
				}
				throw new InvalidOperationException("An entry with the same hash has already been added.");
			}

			fileKeys[previousKeyCount + i] = new(hash1, hash2);
		}

		entry = new(ImmutableCollectionsMarshal.AsImmutableArray(fileKeys), entry.Data);
		return file;
	}

	public void AddFile(ReadOnlySpan<byte> keys, ReadOnlySpan<int> keyLengths, ImmutableArray<byte> data)
	{
		var fileKeys = new Key[keyLengths.Length];
		int offset = 0;
		for (int i = 0; i < keyLengths.Length; i++)
		{
			int keyLength = keyLengths[i];
			var key = keys.Slice(offset, keyLength);
			offset += keyLength;

			ulong hash1 = XxHash3.HashToUInt64(key, ExoArchiveNameSeed1);
			ulong hash2 = XxHash3.HashToUInt64(key, 0);

			if (!_hashes.Add(hash2))
			{
				// Rollback registration of the previous keys.
				for (int j = 0; j < i; j++)
				{
					_hashes.Remove(fileKeys[j].Hash2);
				}
				throw new InvalidOperationException("An entry with the same hash has already been added.");
			}

			fileKeys[i] = new(hash1, hash2);
		}

		_files.Add(new(ImmutableCollectionsMarshal.AsImmutableArray(fileKeys), data));
	}

	public async ValueTask SaveAsync(string fileName, CancellationToken cancellationToken)
	{
		const uint BlockSize = 8;

		static byte[] BuildEntries(List<File> sourceEntries)
		{
			int keyCount = 0;

			foreach (var sourceEntry in sourceEntries)
			{
				keyCount += sourceEntry.Keys.Length;
			}

			uint hashTableLength = BitOperations.RoundUpToPowerOf2((uint)keyCount);

		// NB: In the case where the entries would be too tightly packed, it would be better for us to just grow up widen the hash table.
		// In order to do this, we'll do a first pass building the hash table, while keeping track of the number of times we have to skip an entry
		FillHashTable:;
			var buffer = new byte[hashTableLength * Unsafe.SizeOf<ExoArchiveHashTableEntry>()];
			var destinationEntries = MemoryMarshal.Cast<byte, ExoArchiveHashTableEntry>(buffer.AsSpan());
			destinationEntries.Fill(ExoArchiveHashTableEntry.Empty);
			int totalRetryCount = 0;
			int maxRetryCount = 0;
			int collisionCount = 0;
			uint blockIndex = 0;

			foreach (var sourceEntry in sourceEntries)
			{
				foreach (var key in sourceEntry.Keys)
				{
					int referenceEntryIndex = (int)(key.Hash1 & (hashTableLength - 1));
					int i = referenceEntryIndex;
					int retryCount = 0;

					while (true)
					{
						if (destinationEntries[i].BlockIndex + 1 == 0)
						{
							destinationEntries[i] = new(key.Hash2, blockIndex, (uint)sourceEntry.Data.Length);
							break;
						}
						i++;
						retryCount++;
						totalRetryCount++;
						if (i == hashTableLength) i = 0;
						if (i == referenceEntryIndex) throw new InvalidOperationException();
					}

					if (retryCount > 0)
					{
						maxRetryCount = Math.Max(maxRetryCount, retryCount);
						collisionCount++;
					}
				}

				var (q, r) = Math.DivRem((uint)sourceEntry.Data.Length, BlockSize);

				blockIndex = checked(blockIndex + q);

				if (r != 0) blockIndex = checked(blockIndex + 1);
			}

			// We can always change this heuristic to change when the hash table is rebuilt larger.
			if (collisionCount > 1 && (3 * (ulong)collisionCount >= hashTableLength && maxRetryCount > 1 || totalRetryCount / collisionCount > 10))
			{
				hashTableLength <<= 1;
				goto FillHashTable;
			}

			return buffer;
		}

		var entries = BuildEntries(_files);

		using var file = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);

		var header = new byte[Unsafe.SizeOf<ExoArchiveHeader>()];
		Unsafe.As<byte, ExoArchiveHeader>(ref header[0]) = new(0x52414F58, 1, (uint)(entries.Length / Unsafe.SizeOf<ExoArchiveHashTableEntry>()), BlockSize);

		await file.WriteAsync(header, cancellationToken).ConfigureAwait(false);
		await file.WriteAsync(entries, cancellationToken).ConfigureAwait(false);

		var paddingBytes = new byte[BlockSize];

		// Add padding bytes before the data if necessary.
		uint r = (uint)(header.Length + entries.Length) % BlockSize;
		if (r != 0)
		{
			r = BlockSize - r;
			await file.WriteAsync(paddingBytes.AsMemory(0, (int)r), cancellationToken).ConfigureAwait(false);
		}

		foreach (var entry in _files)
		{
			await file.WriteAsync(ImmutableCollectionsMarshal.AsArray(entry.Data), cancellationToken).ConfigureAwait(false);
			r = (uint)entry.Data.Length % BlockSize;
			if (r != 0)
			{
				r = BlockSize - r;
				await file.WriteAsync(paddingBytes.AsMemory(0, (int)r), cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
