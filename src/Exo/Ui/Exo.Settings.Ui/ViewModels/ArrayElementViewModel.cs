using System.Windows.Input;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class ArrayElementViewModel<T> : BindableObject
	where T : IEquatable<T>
{
	private readonly ArrayPropertyViewModel<T> _arrayPropertyViewModel;
	private T _value;

	public ArrayElementViewModel(ArrayPropertyViewModel<T> arrayPropertyViewModel, T initialValue)
	{
		_arrayPropertyViewModel = arrayPropertyViewModel;
		_value = initialValue;
	}

	public T Value
	{
		get => _value;
		set => SetValue(ref _value, value, ChangedProperty.Value);
	}

	public ICommand RemoveCommand => _arrayPropertyViewModel.RemoveCommand;
}
