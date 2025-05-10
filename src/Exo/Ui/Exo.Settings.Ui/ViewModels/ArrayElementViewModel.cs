using System.Windows.Input;
using Exo.Ui;
using WinRT;

namespace Exo.Settings.Ui.ViewModels;

[GeneratedBindableCustomProperty]
internal sealed partial class ArrayElementViewModel<T> : BindableObject
	where T : IEquatable<T>
{
	private readonly ArrayPropertyViewModel<T> _array;
	private T _value;

	public ArrayElementViewModel(ArrayPropertyViewModel<T> arrayPropertyViewModel, T initialValue)
	{
		_array = arrayPropertyViewModel;
		_value = initialValue;
	}

	public T Value
	{
		get => _value;
		set => SetValue(value, true);
	}

	public ICommand RemoveCommand => _array.RemoveCommand;

	internal void SetValue(T value, bool isUserTriggered)
	{
		var oldValue = _value;
		if (!EqualityComparer<T>.Default.Equals(_value, value))
		{
			_value = value;
			NotifyPropertyChanged(ChangedProperty.Value);
			if (isUserTriggered)
			{
				_array.OnValueChanged(this, oldValue, value);
			}
		}
	}
}
