using Exo.Cooling;
using Xunit;

namespace Exo.Core.Tests.Cooling;

public sealed class InterpolatedSegmentControlCurveTests
{
	[Theory]
	[InlineData(-28, (byte)0, 0, (byte)0)]
	[InlineData(-28, (byte)75, 0, (byte)75)]
	[InlineData(-28, (byte)11, 0, (byte)11)]
	[InlineData(0, (byte)3, 0, (byte)3)]
	[InlineData(0, (byte)3, -1, (byte)3)]
	[InlineData(0, (byte)3, 1, (byte)3)]
	[InlineData(int.MaxValue, (byte)3, 1, (byte)3)]
	public void ShouldSupportFixedCurve(int x, byte y, int sampleX, byte sampleY)
	{
		var curve = new InterpolatedSegmentControlCurve<int, byte>([new(x, y)], MonotonicityValidators<byte>.Increasing);

		Assert.Equal(sampleY, curve[sampleX]);
	}

	[Theory]
	[InlineData(0, (byte)0, (byte)0, -1, (byte)0)]
	[InlineData(0, (byte)0, (byte)0, 1, (byte)0)]
	[InlineData(0, (byte)0, (byte)0, 777, (byte)0)]
	[InlineData(0, (byte)1, (byte)0, -1, (byte)0)]
	[InlineData(0, (byte)1, (byte)0, 0, (byte)1)]
	[InlineData(0, (byte)1, (byte)0, 1, (byte)1)]
	[InlineData(0, (byte)1, (byte)0, 777, (byte)1)]
	[InlineData(91, (byte)55, (byte)3, -1, (byte)3)]
	[InlineData(91, (byte)55, (byte)3, 0, (byte)3)]
	[InlineData(91, (byte)55, (byte)3, 1, (byte)3)]
	[InlineData(91, (byte)55, (byte)3, 90, (byte)3)]
	[InlineData(91, (byte)55, (byte)3, 91, (byte)55)]
	[InlineData(91, (byte)55, (byte)3, 777, (byte)55)]
	public void ShouldSupportFixedCurveWithInitialValue(int x, byte y, byte initialY, int sampleX, byte sampleY)
	{
		var curve = new InterpolatedSegmentControlCurve<int, byte>([new(x, y)], initialY, MonotonicityValidators<byte>.Increasing);

		Assert.Equal(sampleY, curve[sampleX]);
	}

	[Fact]
	public void ShouldSupportIdentityLinearInterpolation()
	{
		var curve = new InterpolatedSegmentControlCurve<byte, byte>([new(0, 0), new(255, 255)], MonotonicityValidators<byte>.StrictlyIncreasing);

		for (int i = 0; i < 256; i++)
		{
			Assert.Equal((byte)i, curve[(byte)i]);
		}
	}

	[Fact]
	public void ShouldSupportDecreasingLinearInterpolation()
	{
		var curve = new InterpolatedSegmentControlCurve<byte, byte>([new(0, 255), new(255, 0)], MonotonicityValidators<byte>.StrictlyDecreasing);

		for (int i = 0; i < 256; i++)
		{
			Assert.Equal((byte)(255 - i), curve[(byte)i]);
		}
	}

	[Theory]
	[InlineData((byte)0, (byte)0)]
	[InlineData((byte)1, (byte)1)]
	[InlineData((byte)5, (byte)5)]
	[InlineData((byte)10, (byte)10)]
	[InlineData((byte)11, (byte)12)]
	[InlineData((byte)14, (byte)18)]
	[InlineData((byte)15, (byte)20)]
	[InlineData((byte)16, (byte)23)]
	[InlineData((byte)17, (byte)26)]
	[InlineData((byte)18, (byte)29)]
	[InlineData((byte)19, (byte)32)]
	[InlineData((byte)20, (byte)35)]
	[InlineData((byte)21, (byte)37)]
	[InlineData((byte)22, (byte)40)]
	[InlineData((byte)29, (byte)57)]
	[InlineData((byte)30, (byte)60)]
	[InlineData((byte)40, (byte)80)]
	[InlineData((byte)45, (byte)100)]
	[InlineData((byte)46, (byte)100)]
	[InlineData((byte)255, (byte)100)]
	public void ShouldInterpolateOnSampleAnchoredMonotonicCurve(byte x, byte y)
	{
		var curve = new InterpolatedSegmentControlCurve<byte, byte>([new(0, 0), new(10, 10), new(15, 20), new(20, 35), new(30, 60), new(40, 80), new(45, 100)], MonotonicityValidators<byte>.StrictlyIncreasingUpTo100);
		Assert.Equal(y, curve[x]);
	}
}
