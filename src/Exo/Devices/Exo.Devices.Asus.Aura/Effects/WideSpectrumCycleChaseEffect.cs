using System.Runtime.Serialization;
using Exo.Lighting.Effects;

namespace Exo.Devices.Asus.Aura.Effects;

[DataContract]
[TypeId(0x17325DE9, 0x9572, 0x41C7, 0xA1, 0xE7, 0x8D, 0x1B, 0x2A, 0x28, 0xEA, 0x30)]
public readonly partial struct WideSpectrumCycleChaseEffect : ISingletonLightingEffect
{
	public static ISingletonLightingEffect SharedInstance { get; } = new SpectrumCyclePulseEffect();
}
