using System.Collections.ObjectModel;

namespace AnyLayout
{
    internal sealed class KeyboardPhysicalLayoutViewModel : BindableObject
    {
        private int _width;
        private int _height;

        public ObservableCollection<KeyShapeViewModel> KeyShapes { get; } = new ObservableCollection<KeyShapeViewModel>();
        public ObservableCollection<PhysicalKeyViewModel> Keys { get; } = new ObservableCollection<PhysicalKeyViewModel>();

        public int Width
        {
            get => _width;
            set => SetValue(ref _width, value);
        }

        public int Height
        {
            get => _height;
            set => SetValue(ref _height, value);
        }
    }
}
