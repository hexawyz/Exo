using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal abstract class ChangeableBindableObject : BindableObject, IChangeable
{
	public abstract bool IsChanged { get; }

	protected void OnChangeStateChange(bool wasChanged) => OnChangeStateChange(wasChanged, IsChanged);

	protected void OnChangeStateChange(bool wasChanged, bool isChanged)
	{
		if (isChanged != wasChanged) OnChanged();
	}

	protected virtual void OnChanged()
	{
		NotifyPropertyChanged(ChangedProperty.IsChanged);
	}
}
