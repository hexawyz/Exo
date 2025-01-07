using System.Numerics;
using Exo.Settings.Ui.Controls;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class PowerDataPointViewModel<T> : BindableObject, IDataPoint<T, byte>
	where T : struct, INumber<T>
{
	private T _x;
	private byte _y;

	public T X
	{
		get => _x;
		set => SetValue(ref _x, value);
	}

	public byte Y
	{
		get => _y;
		set => SetValue(ref _y, value);
	}

	public PowerDataPointViewModel() { }

	public PowerDataPointViewModel(T x, byte y)
	{
		_x = x;
		_y = y;
	}
}
