using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class SharedMemory : IDisposable
{
	public static SharedMemory Create(string prefix, ulong length)
	{
		ArgumentNullException.ThrowIfNull(prefix);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(length, (ulong)long.MaxValue);

		string name = string.Create
		(
			0 + 32 + prefix.Length,
			prefix,
			static (span, prefix) =>
			{
				//@"Global\".CopyTo(span[..7]);
				prefix.CopyTo(span[0..]);
				RandomNumberGenerator.GetHexString(span[(0 + prefix.Length)..], true);
			}
		);
		return new(name, MemoryMappedFile.CreateNew(name, (long)length, MemoryMappedFileAccess.ReadWrite), length);
	}

	private readonly string _name;
	private readonly MemoryMappedFile _file;
	private readonly ulong _length;

	private SharedMemory(string name, MemoryMappedFile file, ulong length)
	{
		_name = name;
		_file = file;
		_length = length;
	}

	public void Dispose() => _file.Dispose();

	public string Name => _name;
	public ulong Length => _length;

	public Stream CreateReadStream() => _file.CreateViewStream(0, (long)_length, MemoryMappedFileAccess.Read);
	public Stream CreateWriteStream() => _file.CreateViewStream(0, (long)_length, MemoryMappedFileAccess.Write);
}
