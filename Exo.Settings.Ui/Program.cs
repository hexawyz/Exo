using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;

namespace Exo.Settings.Ui;

internal static class Program
{
	[DllImport("Microsoft.ui.xaml.dll")]
	[DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
	private static extern void XamlCheckProcessRequirements();

	[STAThread]
	private static void Main(string[] args)
	{
		WinRT.ComWrappersSupport.InitializeComWrappers();

		bool isRedirect = DecideRedirection();
		if (!isRedirect)
		{
			Microsoft.UI.Xaml.Application.Start((p) =>
			{
				var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
				SynchronizationContext.SetSynchronizationContext(context);
				new App();
			});
		}
	}

	private static void OnActivated(object? sender, AppActivationArguments args)
	{
		App.Current.MainWindow?.Activate();
	}

	private static bool DecideRedirection()
	{
		bool isRedirect = false;

		AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
		ExtendedActivationKind kind = args.Kind;
		var keyInstance = AppInstance.FindOrRegisterForKey("da1a419f-698d-4497-8f84-4374623b9cdc");
		if (keyInstance.IsCurrent)
		{
			keyInstance.Activated += OnActivated;
		}
		else
		{
			isRedirect = true;
			RedirectActivationTo(args, keyInstance);
		}
		return isRedirect;
	}

	private static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
	{
		var redirectSemaphore = new Semaphore(0, 1);
		Task.Run
		(
			async () =>
			{
				await keyInstance.RedirectActivationToAsync(args);
				redirectSemaphore.Release();
			}
		);
		redirectSemaphore.WaitOne();
	}
}
