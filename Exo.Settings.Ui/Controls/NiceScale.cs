namespace Exo.Settings.Ui.Controls;

// See: https://stackoverflow.com/a/16363437
internal static class NiceScale
{
	public static (double Min, double Max, double TickSpacing) Compute(double min, double max, double tickCount = 10)
	{
		double range = MakeNice(max - min, false);
		double tickSpacing = MakeNice(range / (tickCount - 1), true);
		double niceMin = min < 0 ?
			Math.Ceiling(min / tickSpacing) * tickSpacing :
			Math.Floor(min / tickSpacing) * tickSpacing;
		double niceMax = min < 0 ?
			Math.Floor(max / tickSpacing) * tickSpacing :
			Math.Ceiling(max / tickSpacing) * tickSpacing;

		return (niceMin, niceMax, tickSpacing);
	}

	public static double MakeNice(double number, bool round)
	{
		double exponent;
		double fraction;
		double niceFraction;

		exponent = Math.Floor(Math.Log10(number));
		fraction = number / Math.Pow(10, exponent);

		if (round)
			if (fraction < 1.5)
				niceFraction = 1;
			else if (fraction < 3)
				niceFraction = 2;
			else if (fraction < 7)
				niceFraction = 5;
			else
				niceFraction = 10;
		else
			if (fraction <= 1)
				niceFraction = 1;
			else if (fraction <= 2)
				niceFraction = 2;
			else if (fraction <= 5)
				niceFraction = 5;
			else
				niceFraction = 10;

		return niceFraction * Math.Pow(10, exponent);
	}
}
