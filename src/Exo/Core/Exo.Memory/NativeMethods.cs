using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace Exo.Memory;

[SuppressUnmanagedCodeSecurity]
internal static unsafe class NativeMethods
{
	public const uint NtStatusBufferTooSmall = 0xC0000023;

	private const uint ReadControl = 0x00020000U;
	private const uint StandardRightsRead = ReadControl;
	private const uint TokenQuery = 0x0008;

	public const string SeCreateGlobalPrivilege = "SeCreateGlobalPrivilege";

	public enum TokenInformationClass : uint
	{
		TokenUser = 1,
		TokenGroups,
		TokenPrivileges,
		TokenOwner,
		TokenPrimaryGroup,
		TokenDefaultDacl,
		TokenSource,
		TokenType,
		TokenImpersonationLevel,
		TokenStatistics,
		TokenRestrictedSids,
		TokenSessionId,
		TokenGroupsAndPrivileges,
		TokenSessionReference,
		TokenSandBoxInert,
		TokenAuditPolicy,
		TokenOrigin,
		TokenElevationType,
		TokenLinkedToken,
		TokenElevation,
		TokenHasRestrictions,
		TokenAccessInformation,
		TokenVirtualizationAllowed,
		TokenVirtualizationEnabled,
		TokenIntegrityLevel,
		TokenUIAccess,
		TokenMandatoryPolicy,
		TokenLogonSid,
		TokenIsAppContainer,
		TokenCapabilities,
		TokenAppContainerSid,
		TokenAppContainerNumber,
		TokenUserClaimAttributes,
		TokenDeviceClaimAttributes,
		TokenRestrictedUserClaimAttributes,
		TokenRestrictedDeviceClaimAttributes,
		TokenDeviceGroups,
		TokenRestrictedDeviceGroups,
		TokenSecurityAttributes,
		TokenIsRestricted,
		TokenProcessTrustLevel,
		TokenPrivateNameSpace,
		TokenSingletonAttributes,
		TokenBnoIsolation,
		TokenChildProcessFlags,
		TokenIsLessPrivilegedAppContainer,
		TokenIsSandboxed,
		TokenIsAppSilo,
		TokenLoggingInformation,
		MaxTokenInfoClass,
	}

	public readonly struct LuidAndAttributes
	{
#pragma warning disable CS0649
		public readonly Luid Luid;
		public readonly PrivilegeAttributes Attributes;
#pragma warning restore CS0649
	}

	public readonly struct Luid : IEquatable<Luid>
	{
		public readonly uint LowPart;
		public readonly uint HighPart;

		public override bool Equals(object? obj) => obj is Luid luid && Equals(luid);
		public bool Equals(Luid other) => LowPart == other.LowPart && HighPart == other.HighPart;
		public override int GetHashCode() => HashCode.Combine(LowPart, HighPart);

		public static bool operator ==(Luid left, Luid right) => left.Equals(right);
		public static bool operator !=(Luid left, Luid right) => !(left == right);
	}

	[Flags]
	public enum PrivilegeAttributes : uint
	{
		Disabled = 0x00000000,
		EnabledByDefault = 0x00000001,
		Enabled = 0x00000002,
		Removed = 0x00000004,
		UsedForAccess = 0x80000000,
	}

	[DllImport("advapi32", CharSet = CharSet.Unicode, EntryPoint = "LookupPrivilegeValueW", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
	private static extern uint LookupPrivilegeValue(string? systemName, string name, Luid* privilege);

	[DllImport("advapi32", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
	private static extern uint OpenProcessToken(nint processHandle, uint desiredAccess, out nint tokenHandle);

	[DllImport("ntdll", ExactSpelling = true, PreserveSig = true, SetLastError = false)]
	private static extern uint NtQueryInformationToken(nint tokenHandle, TokenInformationClass tokenInformationClass, void* tokenInformation, uint tokenInformationLength, uint* returnLength);

	[DllImport("ntdll", ExactSpelling = true, PreserveSig = true, SetLastError = false)]
	private static extern uint RtlNtStatusToDosError(uint status);

	[DllImport("kernel32", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
	private static extern uint CloseHandle(nint handle);

	[DebuggerHidden]
	[StackTraceHidden]
	public static void ValidateNtStatus(uint status)
	{
		if (status != 0)
		{
			throw new Win32Exception((int)RtlNtStatusToDosError(status));
		}
	}

	public static Luid GetPrivilegeValue(string privilegeName)
	{
		ArgumentNullException.ThrowIfNull(privilegeName);

		Luid value = default;
		if (LookupPrivilegeValue(null, privilegeName, &value) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}

		return value;
	}

	public static unsafe LuidAndAttributes[] GetProcessPrivileges()
	{
		if (OpenProcessToken(Process.GetCurrentProcess().Handle, StandardRightsRead | TokenQuery, out nint tokenHandle) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
		try
		{
			Span<byte> data = stackalloc byte[2048];
			uint writtenLength = 0;
			uint result = NtQueryInformationToken(tokenHandle, TokenInformationClass.TokenPrivileges, Unsafe.AsPointer(ref data[0]), (uint)data.Length, &writtenLength);
			if (result != 0)
			{
				Marshal.ThrowExceptionForHR((int)RtlNtStatusToDosError(result));
			}
			return MemoryMarshal.Cast<byte, LuidAndAttributes>(data[..(int)writtenLength].Slice(4, (int)(Unsafe.As<byte, uint>(ref data[0]) * sizeof(LuidAndAttributes)))).ToArray();
		}
		finally
		{
			if (CloseHandle(tokenHandle) == 0)
			{
				Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
			}
		}
	}
}
