namespace Exo.Devices.Nzxt.Kraken;

public partial class KrakenDriver
{
	// Most effects have an "automatic" and an "addressable" version, so hopefully, we should be able to reference all effects using these "automatic" IDs.
	private enum KrakenEffect
	{
		Static = 0x00,
		Fade = 0x01,
		SpectrumWave = 0x02,
		Marquee = 0x03,
		CoveringMarquee = 0x04,
		Alternating = 0x05,
		Pulse = 0x06,
		Breathing = 0x07,
		Candle = 0x08,
		StarryNight = 0x09,
		Blink = 0x0a,
		RainbowWave = 0x0B,
		SuperRainbow = 0x0C,
		RainbowImpulse = 0x0D,
		TaiChi = 0x0E,
		LiquidCooler = 0x0F,
		Loading = 0x10,
	}
}
