using System.Collections.ObjectModel;
using Exo.Lighting;

namespace Exo.Settings.Ui.ViewModels;

internal abstract class FixedLengthArrayPropertyViewModel : PropertyViewModel
{
	private readonly object?[] _values;
	private readonly object?[] _initialValues;
	private int _changedValueCount;

	public ReadOnlyCollection<ArrayElementViewModel> Items { get; }

	public override bool IsChanged => _changedValueCount != 0;

	public FixedLengthArrayPropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength, object? defaultItemValue)
		: base(propertyInformation, paddingLength)
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

	public override int ReadInitialValue(ReadOnlySpan<byte> data)
	{
		int itemSize = ItemSize;

		if (data.Length < PropertyInformation.ArrayLength.GetValueOrDefault() * ItemSize)
		{
			throw new EndOfStreamException("Not enough data.");
		}

		bool wasChanged = IsChanged;
		int offset = 0;
		for (int i = 0; i < _initialValues.Length; i++, offset += itemSize)
		{
			var newInitialValue = ReadValue(data.Slice(offset, itemSize));
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
		return offset;
	}

	public override void WriteValue(BinaryWriter writer)
	{
		int itemSize = ItemSize;
		Span<byte> buffer = stackalloc byte[ItemSize];

		for (int i = 0; i < _values.Length; i++)
		{
			WriteValue(buffer, _values[i]);
			writer.Write(buffer);
		}
	}

	protected abstract int ItemSize { get; }

	protected abstract object? ReadValue(ReadOnlySpan<byte> source);
	protected abstract void WriteValue(Span<byte> destination, object? value);

	protected internal abstract bool AreValuesEqual(object? a, object? b);

	public ref object? GetValueRef(int index) => ref _values[index];
}
