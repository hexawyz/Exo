using System.Runtime.Serialization;
using Exo.Lighting.Effects;

namespace Exo.Devices.Asus.Aura.Effects;

[DataContract]
[TypeId(0x1FA781E9, 0x2426, 0x4F06, 0x9B, 0x6E, 0x72, 0x55, 0xEE, 0x02, 0xA4, 0x3A)]
public readonly struct CycleRandomFlashesEffect : ISingletonLightingEffect
{
	public static ISingletonLightingEffect SharedInstance { get; } = new CycleRandomFlashesEffect();
}
