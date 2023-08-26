using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal abstract class ChangeableBindableObject : BindableObject
{
	public abstract bool IsChanged { get; }

	protected void OnChangeStateChange(bool wasChanged) => OnChangeStateChange(wasChanged, IsChanged);

	protected void OnChangeStateChange(bool wasChanged, bool isChanged)
	{
		if (isChanged != wasChanged) NotifyPropertyChanged(ChangedProperty.IsChanged);
	}
}
