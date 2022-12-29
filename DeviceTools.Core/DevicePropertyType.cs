using System;

namespace DeviceTools
{
	public readonly struct DevicePropertyType : IEquatable<DevicePropertyType>
	{
		public static DevicePropertyType Empty => new(NativeMethods.DevicePropertyType.Empty);
		public static DevicePropertyType Null => new(NativeMethods.DevicePropertyType.Null);
		public static DevicePropertyType SByte => new(NativeMethods.DevicePropertyType.SByte);
		public static DevicePropertyType Byte => new(NativeMethods.DevicePropertyType.Byte);
		public static DevicePropertyType Int16 => new(NativeMethods.DevicePropertyType.Int16);
		public static DevicePropertyType UInt16 => new(NativeMethods.DevicePropertyType.UInt16);
		public static DevicePropertyType Int32 => new(NativeMethods.DevicePropertyType.Int32);
		public static DevicePropertyType UInt32 => new(NativeMethods.DevicePropertyType.UInt32);
		public static DevicePropertyType Int64 => new(NativeMethods.DevicePropertyType.Int64);
		public static DevicePropertyType UInt64 => new(NativeMethods.DevicePropertyType.UInt64);
		public static DevicePropertyType Single => new(NativeMethods.DevicePropertyType.Float);
		public static DevicePropertyType Double => new(NativeMethods.DevicePropertyType.Double);
		public static DevicePropertyType Decimal => new(NativeMethods.DevicePropertyType.Decimal);
		public static DevicePropertyType Guid => new(NativeMethods.DevicePropertyType.Guid);
		public static DevicePropertyType Currency => new(NativeMethods.DevicePropertyType.Currency);
		public static DevicePropertyType Date => new(NativeMethods.DevicePropertyType.Date);
		public static DevicePropertyType FileTime => new(NativeMethods.DevicePropertyType.FileTime);
		public static DevicePropertyType Boolean => new(NativeMethods.DevicePropertyType.Boolean);
		public static DevicePropertyType String => new(NativeMethods.DevicePropertyType.String);
		//public static DevicePropertyType SecurityDescriptor => new(NativeMethods.DevicePropertyType.SecurityDescriptor);
		//public static DevicePropertyType SecurityDescriptoyString => new(NativeMethods.DevicePropertyType.SecurityDescriptorString);
		public static DevicePropertyType PropertyKey => new(NativeMethods.DevicePropertyType.DevicePropertyKey);
		public static DevicePropertyType PropertyType => new(NativeMethods.DevicePropertyType.DevicePropertyType);
		public static DevicePropertyType Win32Error => new(NativeMethods.DevicePropertyType.Error);
		public static DevicePropertyType NtStatus => new(NativeMethods.DevicePropertyType.NtStatus);
		public static DevicePropertyType StringResource => new(NativeMethods.DevicePropertyType.StringResource);

		public static DevicePropertyType Binary => new(NativeMethods.DevicePropertyType.Binary);
		public static DevicePropertyType StringList => new(NativeMethods.DevicePropertyType.StringList);

		internal NativeMethods.DevicePropertyType Value { get; }

		private DevicePropertyType(NativeMethods.DevicePropertyType value) => Value = value;

		public DevicePropertyElementType ElementType => (DevicePropertyElementType)(Value & NativeMethods.DevicePropertyType.MaskType);
		public bool IsArray => (Value & NativeMethods.DevicePropertyType.Array) != 0;
		public bool IsList => (Value & NativeMethods.DevicePropertyType.List) != 0;

		public DevicePropertyType MakeArray()
		{
			if ((Value & NativeMethods.DevicePropertyType.MaskTypeModifier) != 0)
			{
				throw new InvalidOperationException("Cannot make an array from a complex type.");
			}

			if ((Value & NativeMethods.DevicePropertyType.MaskType) is
				NativeMethods.DevicePropertyType.Empty or
				NativeMethods.DevicePropertyType.Null or
				NativeMethods.DevicePropertyType.String or
				NativeMethods.DevicePropertyType.SecurityDescriptor or
				NativeMethods.DevicePropertyType.SecurityDescriptorString or
				NativeMethods.DevicePropertyType.StringResource)
			{
				throw new InvalidOperationException($"The type {Value & NativeMethods.DevicePropertyType.MaskType} cannot be made into an array.");
			}

			return new DevicePropertyType(NativeMethods.DevicePropertyType.Array | (Value & NativeMethods.DevicePropertyType.MaskType));
		}

		public DevicePropertyType MakeList()
		{
			if ((Value & NativeMethods.DevicePropertyType.MaskTypeModifier) != 0)
			{
				throw new InvalidOperationException("Cannot make a list from a complex type.");
			}

			if ((Value & NativeMethods.DevicePropertyType.MaskType) is not
				NativeMethods.DevicePropertyType.String and not
				NativeMethods.DevicePropertyType.SecurityDescriptorString)
			{
				throw new InvalidOperationException($"The type {Value & NativeMethods.DevicePropertyType.MaskType} cannot be made into a list.");
			}

			return new DevicePropertyType(NativeMethods.DevicePropertyType.Array | (Value & NativeMethods.DevicePropertyType.MaskType));
		}

		internal bool IsFixedLength() =>
			Value switch
			{
				NativeMethods.DevicePropertyType.SByte => true,
				NativeMethods.DevicePropertyType.Byte => true,
				NativeMethods.DevicePropertyType.Int16 => true,
				NativeMethods.DevicePropertyType.UInt16 => true,
				NativeMethods.DevicePropertyType.Int32 => true,
				NativeMethods.DevicePropertyType.UInt32 => true,
				NativeMethods.DevicePropertyType.Int64 => true,
				NativeMethods.DevicePropertyType.UInt64 => true,
				NativeMethods.DevicePropertyType.Float => true,
				NativeMethods.DevicePropertyType.Double => true,
				NativeMethods.DevicePropertyType.Decimal => true,
				NativeMethods.DevicePropertyType.Guid => true,
				NativeMethods.DevicePropertyType.Currency => true,
				NativeMethods.DevicePropertyType.Date => true,
				NativeMethods.DevicePropertyType.FileTime => true,
				NativeMethods.DevicePropertyType.Boolean => true,
				NativeMethods.DevicePropertyType.DevicePropertyKey => true,
				NativeMethods.DevicePropertyType.DevicePropertyType => true,
				NativeMethods.DevicePropertyType.Error => true,
				NativeMethods.DevicePropertyType.NtStatus => true,
				_ => false,
			};

		internal int GetLength() =>
			Value switch
			{
				NativeMethods.DevicePropertyType.SByte => 1,
				NativeMethods.DevicePropertyType.Byte => 1,
				NativeMethods.DevicePropertyType.Int16 => 2,
				NativeMethods.DevicePropertyType.UInt16 => 2,
				NativeMethods.DevicePropertyType.Int32 => 4,
				NativeMethods.DevicePropertyType.UInt32 => 4,
				NativeMethods.DevicePropertyType.Int64 => 8,
				NativeMethods.DevicePropertyType.UInt64 => 8,
				NativeMethods.DevicePropertyType.Float => 4,
				NativeMethods.DevicePropertyType.Double => 8,
				// https://learn.microsoft.com/en-us/windows/win32/api/wtypes/ns-wtypes-decimal-r1
				NativeMethods.DevicePropertyType.Decimal => 16,
				NativeMethods.DevicePropertyType.Guid => 16,
				// https://learn.microsoft.com/en-us/windows/win32/api/wtypes/ns-wtypes-cy-r1
				NativeMethods.DevicePropertyType.Currency => 8,
				NativeMethods.DevicePropertyType.Date => 8,
				// https://learn.microsoft.com/en-us/windows/win32/api/minwinbase/ns-minwinbase-filetime
				NativeMethods.DevicePropertyType.FileTime => 8,
				NativeMethods.DevicePropertyType.Boolean => 1,
				NativeMethods.DevicePropertyType.DevicePropertyKey => 20,
				NativeMethods.DevicePropertyType.DevicePropertyType => 4,
				NativeMethods.DevicePropertyType.Error => 4,
				NativeMethods.DevicePropertyType.NtStatus => 4,
				_ => 0,
			};

		public override bool Equals(object? obj) => obj is DevicePropertyType type && Equals(type);
		public bool Equals(DevicePropertyType other) => Value == other.Value;
#if !NETSTANDARD2_0
		public override int GetHashCode() => HashCode.Combine(Value);
#else
		public override int GetHashCode() => -103378149 + Value.GetHashCode();
#endif

		public static bool operator ==(DevicePropertyType left, DevicePropertyType right) => left.Equals(right);
		public static bool operator !=(DevicePropertyType left, DevicePropertyType right) => !(left == right);
	}
}
