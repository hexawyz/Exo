using System.IO.MemoryMappedFiles;
using Exo.Memory;
using Microsoft.Win32.SafeHandles;

namespace Exo.Service;

public class ImageFile : IDisposable
{
	public static ImageFile Open(string path)
		=> new(File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.None, 0), MemoryMappedFileAccess.Read);

	private readonly MemoryMappedFile _file;
	private readonly ulong _length;

	protected ImageFile(SafeFileHandle handle, MemoryMappedFileAccess access)
	{
		_file = MemoryMappedFile.CreateFromFile(handle, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
		_length = (ulong)RandomAccess.GetLength(handle);
	}

	protected ImageFile(FileStream file, MemoryMappedFileAccess access)
	{
		_file = MemoryMappedFile.CreateFromFile(file, null, file.Length, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
		_length = (ulong)file.Length;
	}

	protected ImageFile(MemoryMappedFile file, ulong length)
	{
		_file = file;
		_length = length;
	}

	public virtual void Dispose() => _file.Dispose();

	public ulong Length => _length;

	private Stream CreateStream(MemoryMappedFileAccess access) => _file.CreateViewStream(0, (long)_length, access);
	public Stream CreateReadStream() => CreateStream(MemoryMappedFileAccess.Read);
	private MemoryMappedFileMemoryManager CreateMemoryManager(MemoryMappedFileAccess access) => new MemoryMappedFileMemoryManager(_file, 0, checked((int)Length), access);
	public MemoryMappedFileMemoryManager CreateMemoryManager() => CreateMemoryManager(MemoryMappedFileAccess.Read);
}
