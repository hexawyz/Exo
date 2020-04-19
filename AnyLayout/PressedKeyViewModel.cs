namespace AnyLayout
{
    internal sealed class PressedKeyViewModel : BindableObject
    {
        private ushort _scanCode;

        public ushort ScanCode
        {
            get => _scanCode;
            set => SetValue(ref _scanCode, value);
        }
    }
}
