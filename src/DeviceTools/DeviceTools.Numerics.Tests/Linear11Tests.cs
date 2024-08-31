using System.Runtime.CompilerServices;
using Xunit;

namespace DeviceTools.Numerics.Tests;

public class Linear11Tests
{
	[Theory]
	[InlineData(-4f, (ushort)0x7FC)]
	[InlineData(-3f, (ushort)0x7FD)]
	[InlineData(-2f, (ushort)0x7FE)]
	[InlineData(-1f, (ushort)0x7FF)]
	[InlineData(0f, (ushort)0)]
	[InlineData(1f, (ushort)1)]
	[InlineData(2f, (ushort)2)]
	[InlineData(3f, (ushort)3)]
	[InlineData(4f, (ushort)4)]
	[InlineData(1023f, (ushort)0x3FF)]
	[InlineData(-1024f, (ushort)0x400)]
	[InlineData(0f, (ushort)0x0800)]
	[InlineData(0f, (ushort)0x1800)]
	[InlineData(0f, (ushort)0x1000)]
	[InlineData(0f, (ushort)0xF800)]
	[InlineData(0f, (ushort)0xF000)]
	[InlineData(0.5f, (ushort)0xF801)]
	[InlineData(0.25f, (ushort)0xF001)]
	[InlineData(0.125f, (ushort)0xE801)]
	public void ShouldConvertToSingle(float expectedValue, ushort rawValue)
	{
		Assert.Equal(expectedValue, (float)Unsafe.BitCast<ushort, Linear11>(rawValue));
	}
}
