namespace DeviceTools
{
	internal readonly struct FixedLengthArray3<T>
	{
		private readonly T _0;
		private readonly T _1;
		private readonly T _2;

		public FixedLengthArray3(T v0, T v1, T v2)
			=> (_0, _1, _2) = (v0, v1, v2);
	}
}
