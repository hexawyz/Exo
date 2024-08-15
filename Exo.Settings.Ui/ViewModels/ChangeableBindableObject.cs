using System.ComponentModel;
using System.Runtime.CompilerServices;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal abstract class ChangeableBindableObject : BindableObject, IChangeable
{
	public abstract bool IsChanged { get; }

	protected void OnChangeStateChange(bool wasChanged) => OnChangeStateChange(wasChanged, IsChanged);

	protected void OnChangeStateChange(bool wasChanged, bool isChanged)
	{
		if (isChanged != wasChanged) OnChanged(isChanged);
	}

	protected virtual void OnChanged(bool isChanged)
	{
		NotifyPropertyChanged(ChangedProperty.IsChanged);
	}

	protected bool SetChangeableValue<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
	{
		bool wasChanged = IsChanged;
		bool isChanged = SetValue(ref storage, value, propertyName);
		OnChangeStateChange(wasChanged);
		return isChanged;
	}

	protected bool SetChangeableValue<T>(ref T storage, T value, IEqualityComparer<T> equalityComparer, [CallerMemberName] string? propertyName = null)
	{
		bool wasChanged = IsChanged;
		bool isChanged = SetValue(ref storage, value, equalityComparer, propertyName);
		OnChangeStateChange(wasChanged);
		return isChanged;
	}

	protected bool SetChangeableValue<T>(ref T storage, T value, PropertyChangedEventArgs e)
	{
		bool wasChanged = IsChanged;
		bool isChanged = SetValue(ref storage, value, e);
		OnChangeStateChange(wasChanged);
		return isChanged;
	}

	protected bool SetChangeableValue<T>(ref T storage, T value, IEqualityComparer<T> equalityComparer, PropertyChangedEventArgs e)
	{
		bool wasChanged = IsChanged;
		bool isChanged = SetValue(ref storage, value, equalityComparer, e);
		OnChangeStateChange(wasChanged);
		return isChanged;
	}
}
