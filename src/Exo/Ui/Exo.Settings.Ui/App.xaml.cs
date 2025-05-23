using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Ipc;
using Exo.Service;
using Exo.Service.Ipc;
using Exo.Settings.Ui.Services;
using Exo.Settings.Ui.ViewModels;
using Exo.Ui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Globalization;
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

	private static readonly UnboundedChannelOptions UnboundedChannelOptions = new UnboundedChannelOptions() { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false };

	private Window? _window;
	private readonly ILoggerFactory _loggerFactory;
	private readonly ITypedLoggerProvider _loggerProvider;
	private readonly ILogger<App> _logger;
	private readonly RasterizationScaleController _rasterizationScaleController;
	private readonly CancellationTokenSource _cancellationTokenSource;

	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		_loggerFactory = System.Diagnostics.Debugger.IsAttached
			? new LoggerFactory([new DebugLoggerProvider()], new LoggerFilterOptions() { MinLevel = LogLevel.Debug })
			: new LoggerFactory([new EventLogLoggerProvider(new EventLogSettings() { LogName = "Exo", SourceName = "Ui" })], new LoggerFilterOptions() { MinLevel = LogLevel.Information });

		_loggerProvider = new TypedLoggerProvider(_loggerFactory);

		_logger = _loggerProvider.GetLogger<App>();

		_rasterizationScaleController = new();

		_cancellationTokenSource = new();

		Services = ConfigureServices(_loggerProvider, _rasterizationScaleController);

		InitializeComponent();

		DebugSettings.BindingFailed += (sender, args) =>
		{
			_logger.XamlBindingFailed(args.Message);
		};
		DebugSettings.XamlResourceReferenceFailed += (sender, args) =>
		{
			_logger.XamlResourceReferenceFailed(args.Message);
		};
        UnhandledException += (sender, e) =>
        {
			_logger.XamlUnhandledException(e.Message, e.Exception);
        };

		AppDomain.CurrentDomain.UnhandledException += (senser, e) =>
		{
			if (e.ExceptionObject is Exception ex)
			{
				_logger.UnhandledException(ex);
			}
		};
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
		StartPipeClient();
	}

	private async void StartPipeClient()
	{
		try
		{
			// TODO: Find out if/when we should cancel that token. (It will not prevent the app from closing, but it is a bit dirty)
			await Services.GetRequiredService<ExoUiPipeClient>().StartAsync(_cancellationTokenSource.Token);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (ObjectDisposedException)
		{
			return;
		}
		catch
		{
			Exit();
			return;
		}
	}

	private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
	{
		_rasterizationScaleController.RasterizationScale = sender.RasterizationScale;
	}

	public new static App Current => (App)Application.Current;

	public Window? MainWindow => _window;

	public IServiceProvider Services { get; }

	private static IServiceProvider ConfigureServices(ITypedLoggerProvider loggerProvider, IRasterizationScaleProvider rasterizationScaleProvider)
	{
		var services = new ServiceCollection();

		services.AddSingleton(loggerProvider);

		services.AddSingleton(sp => App.Current.MainWindow!);

		services.AddSingleton<IEditionService, EditionService>();

		services.AddSingleton<ConnectionViewModel>();

		services.AddSingleton(rasterizationScaleProvider);

		services.AddSingleton<MetadataService>();
		services.AddSingleton<ISettingsMetadataService>(sp => sp.GetRequiredService<MetadataService>());

		services.AddSingleton<IFileOpenDialog, FileOpenDialog>();
		services.AddSingleton<IFileSaveDialog, FileSaveDialog>();

		services.AddSingleton(_ => new ResettableChannel<MetadataSourceChangeNotification>(UnboundedChannelOptions));
		services.AddSingleton(_ => new ResettableChannel<MenuChangeNotification>(UnboundedChannelOptions));
		services.AddSingleton(_ => new ResettableChannel<SensorDeviceInformation>(UnboundedChannelOptions));
		services.AddSingleton(_ => new ResettableChannel<SensorConfigurationUpdate>(UnboundedChannelOptions));

		services.AddSingleton<SettingsViewModel>();
		services.AddSingleton<IServiceClient, ExoServiceClient>();

		var dispatcher = DispatcherQueue.GetForCurrentThread();

		services.AddSingleton(dispatcher);

		services.AddSingleton
		(
			sp => new ExoUiPipeClient
			(
				"Local\\Exo.Service.Ui",
				loggerProvider.GetLogger<ExoUiPipeClientConnection>(),
				sp.GetRequiredService<DispatcherQueue>(),
				sp.GetRequiredService<IServiceClient>()
			)
		);

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

	private sealed class FileSaveDialog : IFileSaveDialog
	{
		private readonly Window _mainWindow;

		public FileSaveDialog(Window mainWindow) => _mainWindow = mainWindow;

		async Task<IPickedFile?> IFileSaveDialog.ChooseAsync(ImmutableArray<(string Description, string Extension)> fileTypes)
		{
			var fileSavePicker = new FileSavePicker();
			foreach (var (description, extension) in fileTypes)
			{
				fileSavePicker.FileTypeChoices.Add(description, [extension]);
			}

			InitializeWithWindow.Initialize(fileSavePicker, WindowNative.GetWindowHandle(_mainWindow));
			return await fileSavePicker.PickSaveFileAsync() is { } file ?
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
		public Task<Stream> OpenForWriteAsync() => _file.OpenStreamForWriteAsync();
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
