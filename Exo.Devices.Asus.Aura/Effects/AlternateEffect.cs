using System.Runtime.Serialization;
using Exo.Lighting.Effects;

namespace Exo.Devices.Asus.Aura.Effects;

[DataContract]
[TypeId(0xEDD773B0, 0x727E, 0x4C12, 0xB9, 0x2A, 0xDA, 0x05, 0x5A, 0xCE, 0x49, 0x91)]
public readonly struct AlternateEffect : ISingletonLightingEffect
{
	public static ISingletonLightingEffect SharedInstance { get; } = new AlternateEffect();
}
