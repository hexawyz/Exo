using System.Runtime.Serialization;

namespace Exo.Contracts;

/// <summary>Data types used and supported in the service and UI.</summary>
[DataContract]
public enum DataType : byte
{
	/// <summary>Indicates an unsupported data type.</summary>
	[EnumMember]
	Other = 0,

	[EnumMember]
	UInt8,
	[EnumMember]
	Int8,
	[EnumMember]
	UInt16,
	[EnumMember]
	Int16,
	[EnumMember]
	UInt32,
	[EnumMember]
	Int32,
	[EnumMember]
	UInt64,
	[EnumMember]
	Int64,
	[EnumMember]
	Float16,
	[EnumMember]
	Float32,
	[EnumMember]
	Float64,
	[EnumMember]
	Boolean,
	[EnumMember]
	Guid,
	[EnumMember]
	TimeSpan,
	[EnumMember]
	DateTime,
	[EnumMember]
	String,

	// Semantic data types that are needed for the effect system.

	[EnumMember]
	ColorGrayscale8,
	[EnumMember]
	ColorGrayscale16,
	[EnumMember]
	ColorRgb24,
	[EnumMember]
	ColorRgbw32,
	[EnumMember]
	ColorArgb32,

	[EnumMember]
	ArrayOfColorGrayscale8,
	[EnumMember]
	ArrayOfColorGrayscale16,
	[EnumMember]
	ArrayOfColorRgb24,
	[EnumMember]
	ArrayOfColorRgbw32,
	[EnumMember]
	ArrayOfColorArgb32,
}
