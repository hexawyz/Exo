using System.Buffers;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Exo.Contracts.Ui.Settings;
using Exo.Memory;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class ImagesViewModel : BindableObject, IConnectedState, IDisposable
{
	private static class Commands
	{
		public sealed class AddImageCommand : ICommand
		{
			private readonly ImagesViewModel _viewModel;

			public AddImageCommand(ImagesViewModel viewModel) => _viewModel = viewModel;

			public event EventHandler? CanExecuteChanged;

			public bool CanExecute(object? parameter) => _viewModel.CanAddImage;

			public async void Execute(object? parameter)
			{
				try
				{
					await _viewModel.AddImageAsync(default);
				}
				catch
				{
				}
			}

			public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private static readonly SearchValues<char> NameAllowedCharacters = SearchValues.Create("+-0123456789=ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz");

	public static bool IsNameValid(ReadOnlySpan<char> name) => name.Length > 0 && !name.ContainsAnyExcept(NameAllowedCharacters);

	private readonly ObservableCollection<ImageViewModel> _images;
	private readonly ReadOnlyObservableCollection<ImageViewModel> _readOnlyImages;
	private readonly Commands.AddImageCommand _addImageCommand;

	private string? _loadedImageName;
	private SharedMemory? _loadedImageData;

	private readonly SettingsServiceConnectionManager _connectionManager;
	private IImageService? _imageService;
	private CancellationTokenSource? _cancellationTokenSource;
	private readonly IDisposable _stateRegistration;

	public ImagesViewModel(SettingsServiceConnectionManager connectionManager)
	{
		_images = new();
		_readOnlyImages = new(_images);
		_connectionManager = connectionManager;
		_addImageCommand = new(this);
		_cancellationTokenSource = new();
		_stateRegistration = connectionManager.RegisterStateAsync(this).GetAwaiter().GetResult();
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;
		cts.Cancel();
		_stateRegistration.Dispose();
	}

	public ReadOnlyObservableCollection<ImageViewModel> Images => _readOnlyImages;
	public ICommand AddImageCommand => _addImageCommand;

	async Task IConnectedState.RunAsync(CancellationToken cancellationToken)
	{
		if (_cancellationTokenSource is not { } cts || cts.IsCancellationRequested) return;
		using (var cts2 = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken))
		{
			var imageService = await _connectionManager.GetImageServiceAsync(cts2.Token);
			_imageService = imageService;
			await WatchImagesAsync(imageService, cts2.Token);
		}
	}

	void IConnectedState.Reset()
	{
		_imageService = null;
		_images.Clear();
	}

	// ⚠️ We want the code of this async method to always be synchronized to the UI thread. No ConfigureAwait here.
	private async Task WatchImagesAsync(IImageService imageService, CancellationToken cancellationToken)
	{
		await foreach (var notification in imageService.WatchImagesAsync(cancellationToken))
		{
			switch (notification.NotificationKind)
			{
			case Contracts.Ui.WatchNotificationKind.Enumeration:
			case Contracts.Ui.WatchNotificationKind.Addition:
				_images.Add(new ImageViewModel(notification.Details));
				break;
			case Contracts.Ui.WatchNotificationKind.Removal:
				int i;
				for (i = 0; i < _images.Count; i++)
				{
					if (string.Equals(notification.Details.ImageName, _images[i].Name, StringComparison.OrdinalIgnoreCase))
					{
						_images.RemoveAt(i);
						break;
					}
				}
				break;
			}
		}
	}

	public string? LoadedImageName
	{
		get => _loadedImageName;
		set
		{
			// Image names will be case insensitive, however, we want to consider all casing changes here.
			if (!string.Equals(_loadedImageName, value, StringComparison.Ordinal))
			{
				bool couldAddImage = CanAddImage;
				_loadedImageName = value;
				NotifyPropertyChanged(ChangedProperty.LoadedImageName);
				if (couldAddImage != CanAddImage) _addImageCommand.NotifyCanExecuteChanged();
			}
		}
	}

	public SharedMemory? LoadedImageData
	{
		get => _loadedImageData;
		private set
		{
			if (value != _loadedImageData)
			{
				bool couldAddImage = CanAddImage;
				_loadedImageData?.Dispose();
				_loadedImageData = value;
				NotifyPropertyChanged(ChangedProperty.LoadedImageData);
				if (couldAddImage != CanAddImage) _addImageCommand.NotifyCanExecuteChanged();
			}
		}
	}

	public void SetImage(string name, SharedMemory data)
	{
		LoadedImageName = name;
		LoadedImageData = data;
	}

	public void ClearImage()
	{
		LoadedImageName = null;
		LoadedImageData = null;
	}

	private bool CanAddImage => _loadedImageName is not null && IsNameValid(_loadedImageName) && _loadedImageData is not null;

	private async Task AddImageAsync(CancellationToken cancellationToken)
	{
		if (_imageService is null || _loadedImageName is null || !IsNameValid(_loadedImageName) || _loadedImageData is null) return;

		await _imageService.AddImageAsync(new() { ImageName = _loadedImageName, SharedMemoryName = _loadedImageData.Name, SharedMemoryLength = _loadedImageData.Length }, cancellationToken);
		_loadedImageName = null;
		_loadedImageData.Dispose();
		_loadedImageData = null;
		NotifyPropertyChanged(ChangedProperty.LoadedImageName);
		NotifyPropertyChanged(ChangedProperty.LoadedImageData);
		_addImageCommand.NotifyCanExecuteChanged();
	}
}

internal sealed partial class ImageViewModel : ApplicableResettableBindableObject
{
	private string _name;
	private readonly string _fileName;
	private readonly ushort _width;
	private readonly ushort _height;
	private readonly ImageFormat _format;
	private readonly bool _isAnimated;

	public override bool IsChanged => false;

	public ImageViewModel(ImageInformation information)
	{
		_name = information.ImageName;
		_fileName = information.FileName;
		_width = information.Width;
		_height = information.Height;
		_format = information.Format;
		_isAnimated = information.IsAnimated;
	}

	public string Name
	{
		get => _name;
		set => SetValue(ref _name, value);
	}

	public string FileName => _fileName;
	public ushort Width => _width;
	public ushort Height => _height;
	public ImageFormat Format => _format;
	public bool IsAnimated => _isAnimated;

	protected override Task ApplyChangesAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
	protected override void Reset() => throw new NotImplementedException();

}
