using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exo.Lighting
{
	/// <summary>A predefined RGB light effect that can be applied to a light zone.</summary>
	/// <remarks>
	/// <para>
	/// This enumeration is mirrored by <see cref="SupportedWellKnownLightEffects"/> to identify supported light effects.
	/// Both enums are and <c>must</c> be kep in sync so that values can be converted from <see cref="WellKnownLightEffect"/> to <see cref="SupportedWellKnownLightEffects"/> or inverse.
	/// </para>
	/// <para>
	/// This enumeration define effects that reflect hardware capabilities.
	/// It include effects than can be supported by differend kinds of zones. Depending on whether the zone is RGB, addressable, or both, the supported modes can vary.
	/// Some predefined effects, such as <see cref="RainbowWave"/> can only apply to zones that are composed of more than one light.
	/// Such zones would usually also be <see cref="Addressable"/>.
	/// </para>
	/// <para>
	/// If the zone supports <see cref="Addressable"/>, then all effects can be emulated with manual control.
	/// On some devices, <see cref="Addressable"/> could be the only supported mode, thus requiring CPU control for all animations.
	/// </para>
	/// </remarks>
	public enum WellKnownLightEffect : byte
	{
		/// <summary>Light disabled.</summary>
		None = 0,
		/// <summary>Light with a static color.</summary>
		Static = 1,
		/// <summary>Pulsing light of a static color.</summary>
		Pulse = 2,
		/// <summary>Flashing light of a static color.</summary>
		Flash = 3,
		/// <summary>Flashing light of a static color.</summary>
		DoubleFlash = 4,
		/// <summary>Light cycling through the color spectrum.</summary>
		RainbowCycle = 5,
		/// <summary>Light cycling through the color spectrum.</summary>
		RainbowWave = 6,
		/// <summary>A non predefined mode set by custom methods.</summary>
		Other = 63,
		/// <summary>Lights composing the zones addressed individually.</summary>
		Addressable = 64,
	}

	/// <summary>Supported RGB light effects that can be applied to one or more leds at once.</summary>
	[Flags]
	public enum SupportedWellKnownLightEffects : ulong
	{
		/// <summary>There is no supported effect.</summary>
		/// <remarks>
		/// <para>This is not equivalent to <see cref="WellKnownLightEffect.None"/> wich is always supported and indicate a disabled light.</para>
		/// <para>
		/// All light zones should at least support either <see cref="Static"/> or <see cref="Addressable"/>.
		/// A light zone supporting no effect would not make sense, as it would only represent a non controllable zone.
		/// </para>
		/// </remarks>
		None = 0,
		Static = 1UL << 0,
		Pulse = 1UL << 1,
		Flash = 1UL << 2,
		DoubleFlash = 1UL << 3,
		RainbowCycle = 1UL << 4,
		RainbowWave = 1UL << 5,
		Addressable = 1UL << 63,
	}
}
