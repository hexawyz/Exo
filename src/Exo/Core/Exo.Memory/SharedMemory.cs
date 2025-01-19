using System.IO.MemoryMappedFiles;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Exo.Memory;

public sealed class SharedMemory : IDisposable
{
	private static readonly bool CanCreateGlobalSharedMemory = DetectCreateGlobalPrivilege();
	private static string DefaultPrefix => CanCreateGlobalSharedMemory ? @"Global\" : @"Local\";

	private static bool DetectCreateGlobalPrivilege()
	{
		var seCreateGlobalPrivilege = NativeMethods.GetPrivilegeValue(NativeMethods.SeCreateGlobalPrivilege);
		foreach (var privilege in NativeMethods.GetProcessPrivileges())
		{
			if (privilege.Luid == seCreateGlobalPrivilege && (privilege.Attributes & NativeMethods.PrivilegeAttributes.Enabled) != 0) return true;
		}
		return false;
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
		var memoryMappedFile = MemoryMappedFile.CreateNew(name, (long)length, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.None, HandleInheritability.None);
		// When creating a memory mapped file in the GLobal namespace, we want to adjust the privileges to allow non privileged clients to access it.
		if (CanCreateGlobalSharedMemory)
		{
			// This is a dirty workaround to override security of the memory mapped file, as security for memory mapped files has not been ported to .NET Core ☹️
			// Found here: https://github.com/dotnet/runtime/issues/48793#issuecomment-2299577613
			using (var mutex = new Mutex())
			{
				mutex.SafeWaitHandle.Close();
				mutex.SafeWaitHandle = new SafeWaitHandle(memoryMappedFile.SafeMemoryMappedFileHandle.DangerousGetHandle(), false);

#pragma warning disable CA1416 // Validate platform compatibility
				var security = mutex.GetAccessControl();
				security.AddAccessRule
				(
					new MutexAccessRule
					(
						new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
						// Read + Write
						(MutexRights)6,
						AccessControlType.Allow
					)
				);
				mutex.SetAccessControl(security);
#pragma warning restore CA1416 // Validate platform compatibility
			}
		}
		return new(name, memoryMappedFile, length);
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
