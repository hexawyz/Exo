using System.Windows.Input;
using AnyLayout.RawInput;

namespace AnyLayout
{
    internal sealed class PressedKeyViewModel : BindableObject
    {
        private ushort _scanCode;
        private VirtualKey _key;

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
    }
}
