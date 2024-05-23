namespace Exo.Settings.Ui.ViewModels;

internal abstract class ResettableBindableObject : ChangeableBindableObject, IResettable
{
	protected abstract void Reset();

	protected override void OnChanged()
	{
		IResettable.NotifyCanExecuteChanged();
		base.OnChanged();
	}

	void IResettable.Reset() => Reset();
}
