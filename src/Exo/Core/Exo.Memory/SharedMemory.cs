using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;

namespace Exo.Memory;

public sealed class SharedMemory : IDisposable
{
	private static readonly string DefaultPrefix = GetAcceptablePrefix();

	private static string GetAcceptablePrefix()
	{
		var seCreateGlobalPrivilege = NativeMethods.GetPrivilegeValue(NativeMethods.SeCreateGlobalPrivilege);
		foreach (var privilege in NativeMethods.GetProcessPrivileges())
		{
			if (privilege.Luid == seCreateGlobalPrivilege) return @"Global\";
		}
		return @"Local\";
	}

	public static SharedMemory Create(string prefix, ulong length)
	{
		ArgumentNullException.ThrowIfNull(prefix);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(length, (ulong)long.MaxValue);

		string name = string.Create
		(
			DefaultPrefix.Length + 32 + prefix.Length,
			prefix,
			static (span, prefix) =>
			{
				DefaultPrefix.CopyTo(span[..DefaultPrefix.Length]);
				prefix.CopyTo(span[DefaultPrefix.Length..]);
				RandomNumberGenerator.GetHexString(span[(DefaultPrefix.Length + prefix.Length)..], true);
			}
		);
		return new(name, MemoryMappedFile.CreateNew(name, (long)length, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.None, HandleInheritability.None), length);
	}

	public static SharedMemory Open(string name, ulong length, MemoryMappedFileAccess access)
	{
		ArgumentNullException.ThrowIfNull(name);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(length, (ulong)long.MaxValue);

		return new(name, MemoryMappedFile.CreateOrOpen(name, (long)length, access), length);
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

	public Stream CreateStream(MemoryMappedFileAccess access) => _file.CreateViewStream(0, (long)_length, access);
	public Stream CreateReadStream() => CreateStream(MemoryMappedFileAccess.Read);
	public Stream CreateWriteStream() => CreateStream(MemoryMappedFileAccess.Write);
	public MemoryMappedFileMemoryManager CreateMemoryManager(MemoryMappedFileAccess access) => new MemoryMappedFileMemoryManager(_file, 0, checked((int)Length), access);
}
