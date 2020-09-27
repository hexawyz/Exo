using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DeviceTools.DisplayDevices.Mccs
{
	public readonly struct VcpCommandDefinition
	{
		// TODO: Allow overriding the behavior to support custom VCP code sets.
		public static VcpCommandDefinition CreateForStandardVcpCode(byte vcpCode, string? overridenName, ImmutableArray<ValueDefinition> nonContinuousValues)
		{
			if (((VcpCode)vcpCode).TryGetNameAndCategory(out string? name, out string? category))
			{
				name = overridenName ?? name;
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
}
