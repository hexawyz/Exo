using System.Runtime.Serialization;

namespace Exo.Programming;

[DataContract]
public enum TypeKind
{
	/// <summary>Identifies an externally defined type.</summary>
	/// <remarks>
	/// Modules may use those types to represent types whose values can't be expressed in the programming module.
	/// Opaque types can optionally expose fields, but they don't need to.
	/// </remarks>
	Opaque = 0,
	/// <summary>An intrinsic type.</summary>
	Intrinsic = 1,
	/// <summary>An enumeration type.</summary>
	Enum = 2,
	/// <summary>A GUID wrapper type.</summary>
	/// <remarks>GUID wrapper types are used to strongly type certain resource IDs and annotate APIs by indicating which resource kind are used.</remarks>
	IdWrapper = 3,
	/// <summary>A nullable type.</summary>
	/// <remarks>
	/// Any type can be made nullable, except other nullable types.
	/// All types default to not be nullable. The underlying type is expressed in <see cref="TypeDefinition.ElementTypeId"/>.
	/// </remarks>
	Nullable = 4,
	/// <summary>An array type.</summary>
	/// <remarks>
	/// Array types allow to create an array of elements from any other type.
	/// The type of array elements is expressed in <see cref="TypeDefinition.ElementTypeId"/>.
	/// </remarks>
	Array = 5,
	/// <summary>A custom object type.</summary>
	/// <remarks>These types are user-defined and can be entirely constructed from within the programming module.</remarks>
	Object = 6,
}
