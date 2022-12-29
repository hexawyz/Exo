namespace DeviceTools
{
	internal readonly struct FixedLengthArray2<T>
	{
		private readonly T _0;
		private readonly T _1;

		public FixedLengthArray2(T v0, T v1)
			=> (_0, _1) = (v0, v1);
	}
}
