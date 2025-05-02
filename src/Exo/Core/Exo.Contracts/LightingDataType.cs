namespace Exo.Contracts;

/// <summary>Data types used and supported for lighting effect parameters.</summary>
public enum LightingDataType : byte
{
	/// <summary>Indicates an unsupported data type.</summary>
	Other = 0,

	UInt8,
	SInt8,

	UInt16,
	SInt16,

	UInt32,
	SInt32,

	UInt64,
	SInt64,

	Float16,
	Float32,
	Float64,

	Boolean,
	Guid,
	TimeSpan,
	DateTime,
	String,

	// Semantic data types that are needed for the effect system.

	ColorGrayscale8,
	ColorGrayscale16,

	ColorRgb24,
	ColorRgbw32,
	ColorArgb32,

	// TODO: Remove. We don't need those. Array length can just always be specified.
	ArrayOfColorGrayscale8,
	ArrayOfColorGrayscale16,
	ArrayOfColorRgb24,
	ArrayOfColorRgbw32,
	ArrayOfColorArgb32,
}
