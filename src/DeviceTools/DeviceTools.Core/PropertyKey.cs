using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DeviceTools
{
	[StructLayout(LayoutKind.Sequential)]
	public readonly struct PropertyKey : IEquatable<PropertyKey>
	{
		private static readonly ConcurrentDictionary<PropertyKey, string?> CanonicalNames = new();

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

					string canonicalName;
					ReadOnlySpan<char> canonicalNameSpan;

#if NET6_0_OR_GREATER
					unsafe
					{
						canonicalNameSpan = MemoryMarshal.CreateReadOnlySpanFromNullTerminated((char*)canonicalNamePointer);
					}
#else
					// We could do better here but… Hey, just update your target framework.
					canonicalName = Marshal.PtrToStringUni(canonicalNamePointer)!;
					canonicalNameSpan = canonicalName.AsSpan();
#endif

					// Property names should be case insensitive (ASQ is case insensitive), but we use an exact match here in case we want to recase some properties. (Hello PrinterURL ?)
					// In such a case, we'd still want to return the true unaltered canonical name.
					// Anyway, what we do here is akin to string interning. We avoid having two different string instances with the same data.
					// As many properties with a canonical name will already be materialized in the object model, we'll already have a string straight out of the assembly metadata.
					if (key.TryGetKnownName(out string? knownName) && canonicalNameSpan.Equals(knownName.AsSpan(), StringComparison.Ordinal))
					{
						canonicalName = knownName;
					}
#if NET6_0_OR_GREATER
					else
					{
						// In the case of .NET 6.0+, we'll even have avoided allocating a new CLR string up to this moment.
						canonicalName = canonicalNameSpan.ToString();
					}
#endif

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

		/// <summary>Gets the name of the property, as it is known within the library.</summary>
		/// <remarks>
		/// For properties with an official canonical name, this should return the same value as <see cref="GetCanonicalName"/>, provided the property is materialized in the object model.
		/// </remarks>
		/// <param name="name">The name of the property, if known.</param>
		/// <returns></returns>
#if !NETSTANDARD2_0
		public bool TryGetKnownName([NotNullWhen(true)] out string? name) =>
#else
		public bool TryGetKnownName(out string? name) =>
#endif
			Properties.TryGetName(this, out name);

		// TODO: Have different format strings to choose between "any valid name", "canonical name" and "just raw name"
		public override string ToString()
		{
			if (TryGetKnownName(out var name))
			{
				return name;
			}

			if (GetCanonicalName(this) is string canonicalName)
			{
				return canonicalName;
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
