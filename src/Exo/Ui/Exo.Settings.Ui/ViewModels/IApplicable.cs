using System.Windows.Input;
using WinRT;

namespace Exo.Settings.Ui.ViewModels;

internal partial interface IApplicable : IChangeable
{
	public static ICommand SharedApplyCommand => Commands.ApplyCommand.Instance;

	private static partial class Commands
	{
		[GeneratedBindableCustomProperty]
		public sealed partial class ApplyCommand : ICommand
		{
			public static readonly ApplyCommand Instance = new();

			private ApplyCommand() { }

			public async void Execute(object? parameter)
			{
				try
				{
					await ((IApplicable)parameter!).ApplyAsync(default);
				}
				catch
				{
				}
			}

			public bool CanExecute(object? parameter) => (parameter as IApplicable)?.CanApply ?? false;

			public event EventHandler? CanExecuteChanged;

			public static void RaiseCanExecuteChanged() => Instance.CanExecuteChanged?.Invoke(Instance, EventArgs.Empty);
		}
	}

	static sealed void NotifyCanExecuteChanged() => Commands.ApplyCommand.RaiseCanExecuteChanged();

	ICommand ApplyCommand => SharedApplyCommand;

	Task ApplyAsync(CancellationToken cancellationToken);

	bool CanApply => IsChanged;
}
