using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DeviceTools.DisplayDevices.Mccs;

[DebuggerDisplay("[{VcpCode,h}] {Category,nq} - {Name,nq}")]
public readonly struct VcpCommandDefinition
{
	// TODO: Allow overriding the behavior to support custom VCP code sets.
	public static VcpCommandDefinition CreateForStandardVcpCode(byte vcpCode, string? overriddenName, ImmutableArray<ValueDefinition> nonContinuousValues)
	{
		if (((VcpCode)vcpCode).TryGetNameAndCategory(out string? name, out string? category))
		{
			name = overriddenName ?? name;
		}

		return new VcpCommandDefinition(vcpCode, category, name, nonContinuousValues);
	}

	public VcpCommandDefinition(byte vcpCode, string? category, string? name, ImmutableArray<ValueDefinition> nonContinuousValues)
	{
		VcpCode = vcpCode;
		Category = category;
		Name = name;
		NonContinuousValues = nonContinuousValues;
	}

	public string? Category { get; }
	public string? Name { get; }
	public byte VcpCode { get; }
	public ImmutableArray<ValueDefinition> NonContinuousValues { get; }
}

public readonly struct VcpCommandDefinition<TVcpCode>
	where TVcpCode : struct, Enum
{
	static VcpCommandDefinition()
	{
		if (Unsafe.SizeOf<TVcpCode>() != sizeof(byte))
		{
			throw new InvalidOperationException("The enumeration used to represent VCP codes must have an underlying type of one byte.");
		}
	}

	public VcpCommandDefinition(byte vcpCode, string? category, string? name, ImmutableArray<ValueDefinition> nonContinuousValues)
	{
		VcpCode = vcpCode;
		Category = category;
		Name = name;
		NonContinuousValues = nonContinuousValues;
	}

	public string? Category { get; }
	public string? Name { get; }
	public byte VcpCode { get; }
	public ImmutableArray<ValueDefinition> NonContinuousValues { get; }

	public static explicit operator VcpCommandDefinition<TVcpCode>(VcpCommandDefinition value)
		=> Unsafe.As<VcpCommandDefinition, VcpCommandDefinition<TVcpCode>>(ref value);

	public static implicit operator VcpCommandDefinition(VcpCommandDefinition<TVcpCode> value)
		=> Unsafe.As<VcpCommandDefinition<TVcpCode>, VcpCommandDefinition>(ref value);
}
