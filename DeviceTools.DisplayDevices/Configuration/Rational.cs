namespace DeviceTools.DisplayDevices.Configuration
{
	public readonly struct Rational
	{
		public Rational(uint numerator, uint denominator)
		{
			Numerator = numerator;
			Denominator = denominator;
		}

		public uint Numerator { get; }
		public uint Denominator { get; }
	}
}