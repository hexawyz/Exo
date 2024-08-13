using System.Windows.Input;

namespace Exo.Settings.Ui.ViewModels;

internal abstract class ApplicableResettableBindableObject : ResettableBindableObject, IResettable, IApplicable
{
	public ICommand ApplyCommand => IApplicable.SharedApplyCommand;

	protected virtual bool CanApply => IsChanged;

	protected abstract Task ApplyChangesAsync(CancellationToken cancellationToken);

	protected override void OnChanged()
	{
		IApplicable.NotifyCanExecuteChanged();
		base.OnChanged();
	}

	bool IApplicable.CanApply => CanApply;
	Task IApplicable.ApplyAsync(CancellationToken cancellationToken) => ApplyChangesAsync(cancellationToken);
}
