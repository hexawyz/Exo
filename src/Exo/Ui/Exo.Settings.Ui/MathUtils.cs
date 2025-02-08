namespace Exo.Settings.Ui;

public static class MathUtils
{
	public static uint Gcd(uint a, uint b)
	{
		while (a != 0 && b != 0)
		{
			if (a > b)
				a %= b;
			else
				b %= a;
		}

		return a | b;
	}
}
