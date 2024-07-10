using System.Windows.Input;

namespace Exo.Settings.Ui.ViewModels;

internal interface IRefreshable
{
	public static ICommand SharedRefreshCommand => Commands.RefreshCommand.Instance;

	private static class Commands
	{
		public sealed class RefreshCommand : ICommand
		{
			public static readonly RefreshCommand Instance = new();

			private RefreshCommand() { }

			public async void Execute(object? parameter)
			{
				try
				{
					await ((IRefreshable)parameter!).RefreshAsync(default);
				}
				catch
				{
				}
			}

			public bool CanExecute(object? parameter) => true;

			public event EventHandler? CanExecuteChanged { add { } remove { } }
		}
	}

	ICommand RefreshCommand => SharedRefreshCommand;

	Task RefreshAsync(CancellationToken cancellationToken);
}
