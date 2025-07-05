using Exo.ColorFormats;
using Xunit;

namespace Exo.Core.Tests.ColorFormats;

public class HsvColorTests
{
	[Theory]
	[InlineData((ushort)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0)]
	[InlineData((ushort)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255)]
	[InlineData((ushort)0, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0)]
	[InlineData((ushort)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0)]
	[InlineData((ushort)510, (byte)255, (byte)255, (byte)0, (byte)255, (byte)0)]
	[InlineData((ushort)765, (byte)255, (byte)255, (byte)0, (byte)255, (byte)255)]
	[InlineData((ushort)1020, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255)]
	[InlineData((ushort)1275, (byte)255, (byte)255, (byte)255, (byte)0, (byte)255)]
	[InlineData((ushort)128, (byte)255, (byte)255, (byte)255, (byte)128, (byte)0)]
	[InlineData((ushort)702, (byte)255, (byte)255, (byte)0, (byte)255, (byte)192)]
	[InlineData((ushort)0, (byte)127, (byte)255, (byte)255, (byte)128, (byte)128)]
	public void ShouldConvertToRgb(ushort hue, byte saturation, byte brightness, byte red, byte green, byte blue)
	{
		Assert.Equal(new RgbColor(red, green, blue), new HsvColor(hue, saturation, brightness).ToRgb());
	}

	[Theory]
	[InlineData((ushort)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0)]
	[InlineData((ushort)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255)]
	[InlineData((ushort)0, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0)]
	[InlineData((ushort)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0)]
	[InlineData((ushort)510, (byte)255, (byte)255, (byte)0, (byte)255, (byte)0)]
	[InlineData((ushort)765, (byte)255, (byte)255, (byte)0, (byte)255, (byte)255)]
	[InlineData((ushort)1020, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255)]
	[InlineData((ushort)1275, (byte)255, (byte)255, (byte)255, (byte)0, (byte)255)]
	[InlineData((ushort)128, (byte)255, (byte)255, (byte)255, (byte)128, (byte)0)]
	[InlineData((ushort)702, (byte)255, (byte)255, (byte)0, (byte)255, (byte)192)]
	[InlineData((ushort)0, (byte)127, (byte)255, (byte)255, (byte)128, (byte)128)]
	public void ShouldConvertFromRgb(ushort hue, byte saturation, byte brightness, byte red, byte green, byte blue)
	{
		Assert.Equal(new HsvColor(hue, saturation, brightness), HsvColor.FromRgb(new RgbColor(red, green, blue)));
	}
}
