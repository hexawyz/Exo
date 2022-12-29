namespace DeviceTools
{
	internal readonly struct FixedLengthArray4<T>
	{
		private readonly T _0;
		private readonly T _1;
		private readonly T _2;
		private readonly T _3;

		public FixedLengthArray4(T v0, T v1, T v2, T v3)
			=> (_0, _1, _2, _3) = (v0, v1, v2, v3);
	}
}
