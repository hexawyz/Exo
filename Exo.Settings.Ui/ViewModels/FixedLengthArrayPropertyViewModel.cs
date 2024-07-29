using System.Collections.ObjectModel;
using Exo.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal abstract class FixedLengthArrayPropertyViewModel : PropertyViewModel
{
	private readonly object?[] _values;
	private readonly object?[] _initialValues;
	private int _changedValueCount;

	public ReadOnlyCollection<ArrayElementViewModel> Items { get; }

	public override bool IsChanged => _changedValueCount != 0;

	public FixedLengthArrayPropertyViewModel(ConfigurablePropertyInformation propertyInformation, object? defaultItemValue)
		: base(propertyInformation)
	{
		if (propertyInformation.ArrayLength is not int length) throw new InvalidOperationException("");
		_values = new object?[length];
		_initialValues = new object?[length];
		var viewModels = new ArrayElementViewModel[length];
		for (int i = 0; i < viewModels.Length; i++)
		{
			_values[i] = _initialValues[i] = defaultItemValue;
			viewModels[i] = new(this, i);
		}
		Items = Array.AsReadOnly(viewModels);
	}

	protected override void Reset()
	{
		bool wasChanged = IsChanged;
		for (int i = 0; i < _initialValues.Length; i++)
		{
			var oldInitialValue = _initialValues[i];
			if (!AreValuesEqual(oldInitialValue, _values[i]))
			{
				_values[i] = oldInitialValue;
				Items[i].OnValueChanged();
			}
		}
		OnChangeStateChange(wasChanged);
	}

	public override void SetInitialValue(DataValue value)
	{
		int itemSize = ItemSize;

		if (value.IsDefault || value.BytesValue is not { } bytes || bytes.Length != PropertyInformation.ArrayLength.GetValueOrDefault() * ItemSize)
		{
			throw new InvalidOperationException("Invalid array length.");
		}

		bool wasChanged = IsChanged;
		for (int i = 0; i < _initialValues.Length; i++)
		{
			int offset = i * itemSize;
			var newInitialValue = ReadValue(bytes.AsSpan(offset, itemSize));
			var oldInitialValue = _initialValues[i];
			if (!AreValuesEqual(oldInitialValue, newInitialValue))
			{
				var currentValue = _values[i];
				// If the new initial value is the same as the current value, it means that the current value has switched from "changed" to "not changed".
				if (AreValuesEqual(currentValue, newInitialValue))
				{
					// Try to avoid multiple boxed references to the same value by using the one currently stored as a current value.
					_initialValues[i] = currentValue;
					// Update the changed value count to reflect that the value has become "not changed".
					_changedValueCount--;
				}
				else
				{
					_initialValues[i] = newInitialValue;

					// If the old initial value was the same as the current value, we update the current value so that it stays "not changed".
					if (AreValuesEqual(oldInitialValue, currentValue))
					{
						_values[i] = newInitialValue;
						// Notify watchers that the current value has changed.
						Items[i].OnValueChanged();
					}
				}
			}
		}
		OnChangeStateChange(wasChanged);
	}

	public override DataValue GetDataValue()
	{
		int itemSize = ItemSize;
		var bytes = new byte[PropertyInformation.ArrayLength.GetValueOrDefault() * ItemSize];

		for (int i = 0; i < _values.Length; i++)
		{
			int offset = i * itemSize;
			WriteValue(bytes.AsSpan(offset, itemSize), _values[i]);
		}

		return new DataValue { BytesValue = bytes };
	}

	protected abstract int ItemSize { get; }

	protected abstract object? ReadValue(ReadOnlySpan<byte> source);
	protected abstract void WriteValue(Span<byte> destination, object? value);

	protected internal abstract bool AreValuesEqual(object? a, object? b);

	public ref object? GetValueRef(int index) => ref _values[index];
}
