using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Exo.Ui;

public abstract class BindableObject : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	protected void NotifyPropertyChanged(PropertyChangedEventArgs e) => PropertyChanged?.Invoke(this, e);

	protected void NotifyPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	protected bool SetValue<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
	{
		if (!EqualityComparer<T>.Default.Equals(value, storage))
		{
			storage = value;
			NotifyPropertyChanged(propertyName);
			return true;
		}
		return false;
	}

	protected bool SetValue<T>(ref T storage, T value, IEqualityComparer<T> equalityComparer, [CallerMemberName] string? propertyName = null)
	{
		if (!(equalityComparer ?? EqualityComparer<T>.Default).Equals(value, storage))
		{
			storage = value;
			NotifyPropertyChanged(propertyName);
			return true;
		}
		return false;
	}

	protected bool SetValue<T>(ref T storage, T value, PropertyChangedEventArgs e)
	{
		if (!EqualityComparer<T>.Default.Equals(value, storage))
		{
			storage = value;
			NotifyPropertyChanged(e);
			return true;
		}
		return false;
	}

	protected bool SetValue<T>(ref T storage, T value, IEqualityComparer<T> equalityComparer, PropertyChangedEventArgs e)
	{
		if (!(equalityComparer ?? EqualityComparer<T>.Default).Equals(value, storage))
		{
			storage = value;
			NotifyPropertyChanged(e);
			return true;
		}
		return false;
	}
}
