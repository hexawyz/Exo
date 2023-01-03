using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DeviceTools
{
	[StructLayout(LayoutKind.Sequential)]
	public readonly struct PropertyKey : IEquatable<PropertyKey>
	{
		private static readonly ConcurrentDictionary<PropertyKey, string> CanonicalNames = new();

		private static string? GetCanonicalName(in PropertyKey key) =>
			CanonicalNames.GetOrAdd
			(
				key,
				key =>
				{
					uint result = NativeMethods.PSGetNameFromPropertyKey(key, out IntPtr canonicalNamePointer);

					if (result != 0)
					{
						if (result == NativeMethods.HResultErrorElementNotFound)
						{
							return null;
						}

						Marshal.ThrowExceptionForHR((int)result);
					}

					string canonicalName = Marshal.PtrToStringUni(canonicalNamePointer)!;
					Marshal.FreeCoTaskMem(canonicalNamePointer);
					return canonicalName;
				}
			);

		public readonly Guid CategoryId;
		public readonly uint PropertyId;

		public PropertyKey(Guid categoryId, uint propertyId)
		{
			CategoryId = categoryId;
			PropertyId = propertyId;
		}

		public string? GetCanonicalName() => GetCanonicalName(this);

		public override string ToString()
		{
			if (GetCanonicalName(this) is string name)
			{
				return name;
			}

#if !NETSTANDARD2_0
			Span<char> buffer = stackalloc char[49];

			CategoryId.TryFormat(buffer, out _, "B");
			buffer[38] = ' ';
			PropertyId.TryFormat(buffer.Slice(39), out int n, default, CultureInfo.InvariantCulture);

			return buffer.Slice(0, 39 + n).ToString();
#else
			return CategoryId.ToString("B") + " " + PropertyId.ToString(CultureInfo.InvariantCulture);
#endif
		}

		public override bool Equals(object? obj) => obj is PropertyKey key && Equals(key);
		public bool Equals(PropertyKey other) => CategoryId.Equals(other.CategoryId) && PropertyId == other.PropertyId;

#if !NETSTANDARD2_0
		public override int GetHashCode() => HashCode.Combine(CategoryId, PropertyId);
#else
		public override int GetHashCode()
		{
			int hashCode = -553669671;
			hashCode = hashCode * -1521134295 + CategoryId.GetHashCode();
			hashCode = hashCode * -1521134295 + PropertyId.GetHashCode();
			return hashCode;
		}
#endif

		public static bool operator ==(PropertyKey left, PropertyKey right) => left.Equals(right);
		public static bool operator !=(PropertyKey left, PropertyKey right) => !(left == right);
	}
}
