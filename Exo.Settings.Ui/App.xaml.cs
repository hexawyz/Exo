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
using Exo.Ui.Contracts;
using Exo.Programming;
using ProtoBuf.Meta;

namespace Exo.Settings.Ui;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
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

		InitializeComponent();
	}

	/// <summary>
	/// Invoked when the application is launched.
	/// </summary>
	/// <param name="args">Details about the launch request and process.</param>
	protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
	{
		_window = new Window();
		_window.SystemBackdrop = new MicaBackdrop();
		_window.ExtendsContentIntoTitleBar = true;
		_window.Content = new RootPage(_window);
		_window.Activate();
	}

	private Window? _window;
}
