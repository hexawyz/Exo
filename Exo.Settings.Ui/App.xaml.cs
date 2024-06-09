using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using ProtoBuf.Grpc.Client;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Exo.Contracts.Ui.Settings;
using Exo.Programming;
using ProtoBuf.Meta;
using Microsoft.Extensions.DependencyInjection;
using Exo.Settings.Ui.Services;
using Exo.Settings.Ui.ViewModels;
using Microsoft.UI;
using System.Runtime.InteropServices;
using Exo.Utils;

namespace Exo.Settings.Ui;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
	[DllImport("kernel32", EntryPoint = "GetModuleHandleW", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
	private static extern nint GetModuleHandle(nint zero);
	[DllImport("user32", EntryPoint = "LoadImageW", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
	private static extern nint LoadImage(nint instanceHandle, nint resourceId, uint type, int cx, int cy, uint flags);

	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		// Adjust protobuf serialization for types that require it.
		foreach (var type in typeof(NamedElement).Assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(NamedElement))))
		{
			var metaType = RuntimeTypeModel.Default[type];

			metaType.Add(1, nameof(NamedElement.Id));
			metaType.Add(2, nameof(NamedElement.Name));
			metaType.Add(3, nameof(NamedElement.Comment));
		}

		GrpcClientFactory.AllowUnencryptedHttp2 = true;

		Services = ConfigureServices();

		InitializeComponent();
	}

	/// <summary>
	/// Invoked when the application is launched.
	/// </summary>
	/// <param name="args">Details about the launch request and process.</param>
	protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
	{
		// NB: Not sure how we are supposed to handle DPI here, as icons are loaded for one dimension at a time 🤷
		// This code loads the default icon size, and it seems to work but I didn't check it deeply.
		nint instanceHandle = GetModuleHandle(0);
		nint icon = LoadImage(instanceHandle, 32512, 1, 0, 0, 0x8040);
		_window = new Window();
		_window.AppWindow.SetIcon(Win32Interop.GetIconIdFromIcon(icon));
		_window.SystemBackdrop = new MicaBackdrop();
		_window.ExtendsContentIntoTitleBar = true;
		_window.Content = new RootPage(_window);
		_window.Activate();
	}

	private Window? _window;

	public new static App Current => (App)Application.Current;

	public Window? MainWindow => _window;

	public IServiceProvider Services { get; }

	private static IServiceProvider ConfigureServices()
	{
		var services = new ServiceCollection();

		services.AddSingleton<IEditionService, EditionService>();

		services.AddSingleton<ConnectionViewModel>();

		services.AddSingleton<Metadata.IMetadataService, MetadataService>();

		services.AddSingleton
		(
			sp => new SettingsServiceConnectionManager
			(
				"Local\\Exo.Service.Configuration",
				100,
	#if DEBUG
				null,
#else
				GitCommitHelper.GetCommitId(typeof(SettingsViewModel).Assembly),
#endif
				sp.GetRequiredService<ConnectionViewModel>().OnConnectionStatusChanged
			)
		);

		services.AddSingleton<SettingsViewModel>();

		return services.BuildServiceProvider();
	}
}
