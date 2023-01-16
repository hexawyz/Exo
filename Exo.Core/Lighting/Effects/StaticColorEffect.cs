namespace Exo.Lighting.Effects
{
	public struct StaticColorEffect : ISingleColorEffect
	{
		public RgbColor Color { get; set; }
	}

	public struct PulseEffect : ISingleColorEffect
	{
		public RgbColor Color { get; set; }
	}

	public struct FlashEffect : ISingleColorEffect
	{
		public RgbColor Color { get; set; }
	}

	public struct AddressableColorEffect : IApplicableColorEffect
	{
		private readonly RgbColor[] _color;
	}
}
