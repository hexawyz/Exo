using AnyLayout.RawInput;

namespace AnyLayout
{
    internal sealed class PressedKeyViewModel : BindableObject
    {
        private ushort _scanCode;
        private VirtualKey _key;
        private uint _extraInformation;

        public ushort ScanCode
        {
            get => _scanCode;
            set => SetValue(ref _scanCode, value);
        }

        public VirtualKey Key
        {
            get => _key;
            set => SetValue(ref _key, value);
        }

        public uint ExtraInformation
        {
            get => _extraInformation;
            set => SetValue(ref _extraInformation, value);
        }
    }
}
