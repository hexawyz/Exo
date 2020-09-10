namespace AnyLayout
{
	internal sealed class PhysicalKeyViewModel : BindableObject
	{
		public KeyShapeViewModel Shape { get; }

		private int _x;
		private int _y;
		private int _scanCode;

		public int X
		{
			get => _x;
			set => SetValue(ref _x, value);
		}

		public int Y
		{
			get => _y;
			set => SetValue(ref _y, value);
		}

		public int ScanCode
		{
			get => _scanCode;
			set => SetValue(ref _scanCode, value);
		}
	}
}
