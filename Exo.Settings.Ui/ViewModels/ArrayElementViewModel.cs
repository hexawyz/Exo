using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class ArrayElementViewModel : BindableObject
{
	private readonly FixedLengthArrayPropertyViewModel _arrayPropertyViewModel;
	private readonly int _index;

	public ArrayElementViewModel(FixedLengthArrayPropertyViewModel arrayPropertyViewModel, int index)
	{
		_arrayPropertyViewModel = arrayPropertyViewModel;
		_index = index;
	}

	public object? Value
	{
		get => _arrayPropertyViewModel.GetValueRef(_index);
		set
		{
			ref object? storage = ref _arrayPropertyViewModel.GetValueRef(_index);

			if (!_arrayPropertyViewModel.AreValuesEqual(storage, value))
			{
				storage = value;
				NotifyPropertyChanged(ChangedProperty.Value);
			}
		}
	}

	internal void OnValueChanged() => NotifyPropertyChanged(ChangedProperty.Value);
}
