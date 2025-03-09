using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.InteropServices;
using Exo.Contracts.Ui;
using Exo.Programming;
using Exo.Settings.Ui.Services;
using Exo.Settings.Ui.ViewModels;
using Exo.Ui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Globalization;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Meta;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

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

		RuntimeTypeModel.Default.Add<UInt128>(false).SerializerType = typeof(UInt128Serializer);

		GrpcClientFactory.AllowUnencryptedHttp2 = true;

		_rasterizationScaleController = new();

		Services = ConfigureServices(_rasterizationScaleController);

		InitializeComponent();
	}

	/// <summary>
	/// Invoked when the application is launched.
	/// </summary>
	/// <param name="args">Details about the launch request and process.</param>
	protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
	{
#if FORCE_LANGUAGE_EN
		string forcedCultureName = "en-US";
#elif FORCE_LANGUAGE_FR
		string forcedCultureName = "fr-FR";
#elif FORCE_LANGUAGE_JA
		string forcedCultureName = "ja-JP";
#endif
#if FORCE_LANGUAGE_EN || FORCE_LANGUAGE_FR || FORCE_LANGUAGE_JA
		var cultureInfo = CultureInfo.GetCultureInfo(forcedCultureName, true);
		CultureInfo.CurrentCulture = cultureInfo;
		CultureInfo.CurrentUICulture = cultureInfo;

		ApplicationLanguages.PrimaryLanguageOverride = forcedCultureName;
#endif

		// NB: Not sure how we are supposed to handle DPI here, as icons are loaded for one dimension at a time 🤷
		// This code loads the default icon size, and it seems to work but I didn't check it deeply.
		nint instanceHandle = GetModuleHandle(0);
		nint icon = LoadImage(instanceHandle, 32512, 1, 0, 0, 0x8040);
		_window = new Window();
		_window.AppWindow.SetIcon(Win32Interop.GetIconIdFromIcon(icon));
		_window.SystemBackdrop = new MicaBackdrop();
		_window.ExtendsContentIntoTitleBar = true;
		var rootPage = new RootPage(_window);
		_window.Content = rootPage;
		_window.Activate();
		rootPage.Loaded += (sender, e) =>
		{
			// Setup logic to track DPI so that we can properly scale images in the UI.
			var xamlRoot = _window.Content.XamlRoot;
			_rasterizationScaleController.RasterizationScale = xamlRoot.RasterizationScale;
			xamlRoot.Changed += OnXamlRootChanged;
		};
	}

	private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
	{
		_rasterizationScaleController.RasterizationScale = sender.RasterizationScale;
	}

	private Window? _window;
	private readonly RasterizationScaleController _rasterizationScaleController;

	public new static App Current => (App)Application.Current;

	public Window? MainWindow => _window;

	public IServiceProvider Services { get; }

	private static IServiceProvider ConfigureServices(IRasterizationScaleProvider rasterizationScaleProvider)
	{
		var services = new ServiceCollection();

		services.AddSingleton(sp => App.Current.MainWindow!);

		services.AddSingleton<IEditionService, EditionService>();

		services.AddSingleton<ConnectionViewModel>();

		services.AddSingleton(rasterizationScaleProvider);

		services.AddSingleton<ISettingsMetadataService, MetadataService>();

		services.AddSingleton<IFileOpenDialog, FileOpenDialog>();

		services.AddSingleton
		(
			sp => new SettingsServiceConnectionManager
			(
				"Local\\Exo.Service.Configuration",
				100,
#if DEBUG
				null,
#else
				Exo.Utils.GitCommitHelper.GetCommitIdString(typeof(SettingsViewModel).Assembly),
#endif
				sp.GetRequiredService<ConnectionViewModel>().OnConnectionStatusChanged
			)
		);

		services.AddSingleton<SettingsViewModel>();

		return services.BuildServiceProvider();
	}

	private sealed class FileOpenDialog : IFileOpenDialog
	{
		private readonly Window _mainWindow;

		public FileOpenDialog(Window mainWindow) => _mainWindow = mainWindow;

		async Task<IPickedFile?> IFileOpenDialog.OpenAsync(ImmutableArray<string> extensions)
		{
			var fileOpenPicker = new FileOpenPicker();
			foreach (var extension in extensions)
			{
				fileOpenPicker.FileTypeFilter.Add(extension);
			}

			InitializeWithWindow.Initialize(fileOpenPicker, WindowNative.GetWindowHandle(_mainWindow));
			return await fileOpenPicker.PickSingleFileAsync() is { } file ?
				new PickedFile(file) :
				null;
		}
	}

	private sealed class PickedFile : IPickedFile
	{
		private readonly StorageFile _file;

		public PickedFile(StorageFile file)
			=> _file = file;

		public string? Path => _file.Path;
		public Task<Stream> OpenForReadAsync() => _file.OpenStreamForReadAsync();
	}

	private sealed class RasterizationScaleController : BindableObject, IRasterizationScaleProvider
	{
		private double _rasterizationScale = 1;

		public double RasterizationScale
		{
			get => _rasterizationScale;
			set => SetValue(ref _rasterizationScale, value, ChangedProperty.RasterizationScale);
		}
	}
}
