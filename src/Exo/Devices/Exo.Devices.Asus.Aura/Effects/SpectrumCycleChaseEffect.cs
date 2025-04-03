using System.Runtime.Serialization;
using Exo.Lighting.Effects;

namespace Exo.Devices.Asus.Aura.Effects;

[DataContract]
[TypeId(0xB815E8FD, 0x7AD5, 0x4A48, 0x8F, 0xDB, 0x04, 0x43, 0x1F, 0x15, 0x98, 0x53)]
public readonly partial struct SpectrumCycleChaseEffect : ISingletonLightingEffect
{
	public static ISingletonLightingEffect SharedInstance { get; } = new SpectrumCyclePulseEffect();
}
