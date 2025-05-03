using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Exo.Lighting;

namespace Exo.Settings.Ui.ViewModels;

internal abstract class ArrayPropertyViewModel<T> : PropertyViewModel
	where T : IEquatable<T>
{
	private static class Commands
	{
		public sealed class AddCommand : ICommand
		{
			private readonly ArrayPropertyViewModel<T> _owner;

			public AddCommand(ArrayPropertyViewModel<T> owner) => _owner = owner;

			bool ICommand.CanExecute(object? parameter) => _owner.CanAddElement;
			void ICommand.Execute(object? parameter) => _owner.AddElement();

			private event EventHandler? CanExecuteChanged;
			event EventHandler? ICommand.CanExecuteChanged
			{
				add => CanExecuteChanged += value;
				remove => CanExecuteChanged += value;
			}

			public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}

		public sealed class RemoveCommand : ICommand
		{
			private readonly ArrayPropertyViewModel<T> _owner;

			public RemoveCommand(ArrayPropertyViewModel<T> owner) => _owner = owner;

			bool ICommand.CanExecute(object? parameter) => _owner.CanRemoveElement;

			void ICommand.Execute(object? parameter)
			{
				if (parameter is ArrayElementViewModel<T> element)
				{
					_owner.RemoveElement(element);
				}
			}

			private event EventHandler? CanExecuteChanged;
			event EventHandler? ICommand.CanExecuteChanged
			{
				add => CanExecuteChanged += value;
				remove => CanExecuteChanged += value;
			}

			public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private readonly T[] _initialValues;
	private readonly ObservableCollection<ArrayElementViewModel<T>> _elements;
	private readonly ReadOnlyObservableCollection<ArrayElementViewModel<T>> _readOnlyElements;
	private readonly Commands.AddCommand _addCommand;
	private readonly Commands.RemoveCommand _removeCommand;
	private readonly T _defaultItemValue;
	private uint _initialValueCount;
	private uint _changedValueCount;

	public ReadOnlyObservableCollection<ArrayElementViewModel<T>> Elements => _readOnlyElements;

	public override bool IsChanged => _changedValueCount != 0;

	public bool IsVariableLength => PropertyInformation.IsVariableLengthArray;

	public ArrayPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength, T[]? initialValues, T defaultItemValue)
		: base(propertyInformation, paddingLength)
	{
		if (!propertyInformation.IsArray) throw new InvalidOperationException("");
		_initialValues = new T[propertyInformation.MaximumElementCount];
		_elements = new();
		_defaultItemValue = defaultItemValue;
		for (int i = 0; i < _initialValues.Length; i++)
		{
			if (initialValues is not null && i < initialValues.Length)
			{
				_elements.Add(new(this, _initialValues[i] = initialValues[i]));
			}
			else
			{
				_initialValues[i] = defaultItemValue;
				if (i < propertyInformation.MinimumElementCount) _elements.Add(new(this, defaultItemValue));
			}
		}
		_readOnlyElements = new(_elements);
		_initialValueCount = initialValues is not null ? (uint)initialValues.Length : propertyInformation.MinimumElementCount;
		_addCommand = new(this);
		_removeCommand = new(this);
	}

	protected override void Reset()
	{
		bool wasChanged = IsChanged;
		for (uint i = 0; i < _initialValueCount; i++)
		{
			var initialValue = _initialValues[i];
			if (i < _elements.Count)
			{
				var element = _elements[(int)i];
				if (!EqualityComparer<T>.Default.Equals(initialValue, element.Value))
				{
					element.SetValue(initialValue, false);
				}
			}
			else
			{
				_elements.Add(new(this, initialValue));
			}
		}
		for (uint i = (uint)_elements.Count; --i >= _initialValueCount;)
		{
			_elements.RemoveAt((int)i);
		}
		_changedValueCount = 0;
		OnChangeStateChange(wasChanged);
	}

	public ICommand AddCommand => _addCommand;
	public ICommand RemoveCommand => _removeCommand;

	private bool CanAddElement => _elements.Count < PropertyInformation.MaximumElementCount;

	private void AddElement()
	{
		if (_elements.Count < PropertyInformation.MaximumElementCount)
		{
			bool wasChanged = IsChanged;
			_elements.Add(new(this, _defaultItemValue));
			// Either we added an element catching up with the set of initial values and that can be one less change,
			// or we added an element past the set of initial values and that's always an additional change.
			if (_elements.Count <= _initialValueCount)
			{
				if (EqualityComparer<T>.Default.Equals(_defaultItemValue, _initialValues[_elements.Count - 1])) _changedValueCount--;
			}
			else
			{
				_changedValueCount++;
			}
			OnChangeStateChange(wasChanged);
			if (_elements.Count == PropertyInformation.MaximumElementCount) _addCommand.RaiseCanExecuteChanged();
			if (_elements.Count == PropertyInformation.MinimumElementCount + 1) _removeCommand.RaiseCanExecuteChanged();
		}
	}

	private bool CanRemoveElement => _elements.Count > PropertyInformation.MinimumElementCount;

	private void RemoveElement(ArrayElementViewModel<T> element)
	{
		bool wasChanged = IsChanged;
		if (!CanRemoveElement || !_elements.Remove(element)) return;
		uint changedValueCount = (uint)Math.Abs(_elements.Count - (int)_initialValueCount);
		uint commonElementCount = Math.Min((uint)_elements.Count, _initialValueCount);
		for (uint i = 0; i < commonElementCount; i++)
		{
			if (!EqualityComparer<T>.Default.Equals(_elements[(int)i].Value, _initialValues[i]))
			{
				changedValueCount++;
			}
		}
		_changedValueCount = changedValueCount;
		if (_elements.Count + 1 == PropertyInformation.MaximumElementCount) _addCommand.RaiseCanExecuteChanged();
		if (!CanRemoveElement) _removeCommand.RaiseCanExecuteChanged();
		OnChangeStateChange(wasChanged);
	}

	protected virtual int ItemSize => Unsafe.SizeOf<T>();

	public override int ReadInitialValue(ReadOnlySpan<byte> data)
	{
		bool wasChanged = IsChanged;
		bool couldAddElement = CanAddElement;
		bool couldRemoveElement = CanRemoveElement;
		uint itemSize = (uint)ItemSize;
		uint elementCount;
		uint offset = 0;

		if (PropertyInformation.IsVariableLengthArray)
		{
			var reader = new BufferReader(data);
			elementCount = reader.ReadVariableUInt32();
			offset = (uint)((uint)data.Length - reader.RemainingLength);
			if (elementCount > (uint)_initialValues.Length) throw new InvalidDataException("The number of values cannot fit into the allocated array.");
		}
		else
		{
			elementCount = PropertyInformation.MinimumElementCount;
		}

		if (data.Length < elementCount * itemSize)
		{
			throw new EndOfStreamException("Not enough data.");
		}

		uint changedElementCount = Math.Min(elementCount, _initialValueCount);
		uint i;
		// First process all items that are guaranteed to exist in both the new and the old "initial value" arrays.
		// This will be the whole of the data for fixed length arrays, but this could be as low as zero items for dynamic arrays.
		for (i = 0; i < changedElementCount; i++, offset += itemSize)
		{
			var newInitialValue = ReadValue(data.Slice((int)offset, (int)itemSize));
			var oldInitialValue = _initialValues[i];
			if (!EqualityComparer<T>.Default.Equals(oldInitialValue, newInitialValue))
			{
				// If the new initial value is the same as the current value, it means that the current value has switched from "changed" to "not changed".
				if (i < _elements.Count && _elements[(int)i] is var element)
				{
					var elementValue = element.Value;
					_initialValues[i] = newInitialValue;
					if (EqualityComparer<T>.Default.Equals(elementValue, newInitialValue))
					{
						// Update the changed value count to reflect that the value has become "not changed".
						_changedValueCount--;
					}
					else
					{
						// If the old initial value was the same as the current value, we update the current value so that it stays "not changed".
						if (EqualityComparer<T>.Default.Equals(oldInitialValue, elementValue))
						{
							element.SetValue(newInitialValue, false);
						}
					}
				}
			}
		}
		if (_initialValueCount != elementCount)
		{
			// We could either have some other new values to process OR old extra values to "remove".
			if (_initialValueCount < elementCount)
			{
				// Add any values that may need to be added.
				for (; i < elementCount; i++, offset += itemSize)
				{
					var newInitialValue = ReadValue(data.Slice((int)offset, (int)itemSize));
					if (i < _elements.Count && _elements[(int)i] is var element)
					{
						var elementValue = element.Value;
						_initialValues[i] = newInitialValue;
						// In this case, since we add a new initial value that has a pre-existing matching element,
						// that element becomes "unchanged" if it matches the new initial value.
						if (EqualityComparer<T>.Default.Equals(elementValue, newInitialValue))
						{
							_changedValueCount--;
						}
					}
					else
					{
						// In this case, since we add a new value that doesn't have a matching element, it counts as a new change.
						_initialValues[i] = newInitialValue;
						_changedValueCount++;
					}
				}
			}
			else
			{
				// Process the removal of any old initial values.
				// We reset the data for convenience when adding new items, but the main point of this is to keep the status up-to-date.
				// Basically, if a value matching these was "unchanged", it automatically becomes "changed".
				for (; i < _initialValueCount; i++)
				{
					if (i < _elements.Count && _elements[(int)i] is var element)
					{
						var elementValue = element.Value;
						if (EqualityComparer<T>.Default.Equals(elementValue, _initialValues[i]))
						{
							// Update the changed value count to reflect that the value has become "changed".
							_changedValueCount++;
						}
					}
					else
					{
						_changedValueCount--;
					}
					_initialValues[i] = _defaultItemValue;
				}
			}
		}
		// Finally, if the array was initially unchanged AND we got a change in the number of initial values, do actually update the live elements to reflect this
		if (!wasChanged && elementCount != _initialValueCount && _initialValueCount == _elements.Count)
		{
			if (elementCount > _elements.Count)
			{
				for (i = (uint)_elements.Count; i < elementCount; i++)
				{
					_elements.Add(new(this, _initialValues[i]));
					_changedValueCount--;
				}
			}
			else
			{
				for (i = (uint)_elements.Count; --i >= elementCount;)
				{
					if (_elements.Count == PropertyInformation.MinimumElementCount)
					{
						throw new InvalidOperationException("Trying to remove too many elements during an update.");
					}
					_elements.RemoveAt((int)i);
					_changedValueCount--;
				}
			}
		}
		bool canAddElementChanged = CanAddElement != couldAddElement;
		bool canRemoveElementChanged = CanRemoveElement != couldRemoveElement;
		_initialValueCount = elementCount;
		OnChangeStateChange(wasChanged);
		if (canAddElementChanged) _addCommand.RaiseCanExecuteChanged();
		if (canRemoveElementChanged) _removeCommand.RaiseCanExecuteChanged();
		return (int)offset;
	}

	public override void WriteValue(BinaryWriter writer)
	{
		int itemSize = ItemSize;
		Span<byte> buffer = stackalloc byte[ItemSize];

		if (PropertyInformation.IsVariableLengthArray)
		{
			writer.Write7BitEncodedInt(_elements.Count);
		}

		foreach (var element in _elements)
		{
			WriteValue(buffer, element.Value);
			writer.Write(buffer);
		}
	}

	internal void OnValueChanged(ArrayElementViewModel<T> element, T oldValue, T newValue)
	{
		int index = _elements.IndexOf(element);
		if (index < 0 || index >= _initialValueCount) return;
		bool wasChanged = IsChanged;
		ref var initialValue = ref _initialValues[index];
		if (EqualityComparer<T>.Default.Equals(initialValue, newValue))
		{
			_changedValueCount--;
		}
		else if (EqualityComparer<T>.Default.Equals(initialValue, oldValue))
		{
			_changedValueCount++;
		}
		OnChangeStateChange(wasChanged);
	}

	protected abstract T ReadValue(ReadOnlySpan<byte> source);
	protected abstract void WriteValue(Span<byte> destination, T value);
}
