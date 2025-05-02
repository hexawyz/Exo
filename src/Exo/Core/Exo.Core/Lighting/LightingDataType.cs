namespace Exo.Lighting;

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

	UInt128,
	SInt128,

	Float16,
	Float32,
	Float64,

	Boolean,
	Guid,
	TimeSpan,
	DateTime,
	String,

	// Semantic data types that are needed for the effect system.

	/// <summary>Represents the direction of an effect moving in one dimension.</summary>
	/// <remarks>
	/// This semantic typing will give the option for a better UI presentation than a generic <see cref="bool"/> would allow.
	/// </remarks>
	EffectDirection1D,
	/// <summary>Represents the direction of an effect moving in two dimensions.</summary>
	EffectDirection2D,
	/// <summary>Represents the direction of an effect moving in three dimensions.</summary>
	EffectDirection3D,

	ColorGrayscale8,
	ColorGrayscale16,

	ColorRgb24,
	ColorRgbw32,
	ColorArgb32,
}
