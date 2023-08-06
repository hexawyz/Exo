using System;
using CommunityToolkit.WinUI.UI.Controls;
using Windows.UI;

namespace Exo.Settings.Ui;

internal sealed class RgbLightingDefaultPalette: IColorPalette
{
	private static readonly Color[,] AllColors = BuildColors();

	private static double ToRgb(byte value)
	{
		double v = value / 255d;

		return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
	}

	private static byte FromRgb(double value)
	{
		double v = value <= 0.0031308 ? 12.92 * value : Math.Pow(1.055 * value - 0.055, 1 / 2.4);
		return (byte)Math.Clamp(255 * v, 0, 255);
	}

	// The generated colors and shades are quite arbitrary at the moment and could probably be improved, but at least they provide some visual results on RGB LED areas.
	// To be fair, we probably need some color calibration system for RGB lighting in order to better match screen colors 😫
	private static Color[,] BuildColors()
	{
		var shades = new double[] { 1d, 0.75d, 0.5d, 0.25d };
		var colors = new Color[25, shades.Length];

		for (int i = 0; i < shades.Length; i++)
		{
			double shade = shades[i];
			byte value = FromRgb(shade);
			byte p30 = FromRgb(0.30 * shade);
			byte p50 = FromRgb(0.50 * shade);
			byte p70 = FromRgb(0.70 * shade);

			colors[0, i] = Color.FromArgb(255, value, 0, 0);
			colors[1, i] = Color.FromArgb(255, value, p30, 0);
			colors[2, i] = Color.FromArgb(255, value, p50, 0);
			colors[3, i] = Color.FromArgb(255, value, p70, 0);
			colors[4, i] = Color.FromArgb(255, value, value, 0);
			colors[5, i] = Color.FromArgb(255, p70, value, 0);
			colors[6, i] = Color.FromArgb(255, p50, value, 0);
			colors[7, i] = Color.FromArgb(255, p30, value, 0);
			colors[8, i] = Color.FromArgb(255, 0, value, 0);
			colors[9, i] = Color.FromArgb(255, 0, value, p30);
			colors[10, i] = Color.FromArgb(255, 0, value, p50);
			colors[11, i] = Color.FromArgb(255, 0, value, p70);
			colors[12, i] = Color.FromArgb(255, 0, value, value);
			colors[13, i] = Color.FromArgb(255, 0, p70, value);
			colors[14, i] = Color.FromArgb(255, 0, p50, value);
			colors[15, i] = Color.FromArgb(255, 0, p30, value);
			colors[16, i] = Color.FromArgb(255, 0, 0, value);
			colors[17, i] = Color.FromArgb(255, p30, 0, value);
			colors[18, i] = Color.FromArgb(255, p50, 0, value);
			colors[19, i] = Color.FromArgb(255, p70, 0, value);
			colors[20, i] = Color.FromArgb(255, value, 0, value);
			colors[21, i] = Color.FromArgb(255, value, 0, p70);
			colors[22, i] = Color.FromArgb(255, value, 0, p50);
			colors[23, i] = Color.FromArgb(255, value, 0, p30);
			colors[24, i] = Color.FromArgb(255, value, value, value);
		}

		return colors;
	}

	private static int ClampIndex(int value, int count) => value >= 0 ? value < count ? value : count : 0;

	public Color GetColor(int colorIndex, int shadeIndex) => AllColors[ClampIndex(colorIndex, AllColors.GetLength(0)), ClampIndex(shadeIndex, AllColors.GetLength(1))];

	public int ColorCount => AllColors.GetLength(0);
	public int ShadeCount => AllColors.GetLength(1);
}
