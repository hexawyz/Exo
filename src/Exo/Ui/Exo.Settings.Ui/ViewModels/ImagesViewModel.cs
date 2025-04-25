using System.Buffers;
using System.Collections.ObjectModel;
using System.Data;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Windows.Input;
using Exo.Memory;
using Exo.Service;
using Exo.Settings.Ui.Services;
using Exo.Ui;
using Microsoft.Extensions.Logging;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class ImagesViewModel : BindableObject, IDisposable
{
	private static class Commands
	{
		public sealed class OpenImageCommand : ICommand
		{
			private readonly ImagesViewModel _viewModel;

			public OpenImageCommand(ImagesViewModel viewModel) => _viewModel = viewModel;

			public event EventHandler? CanExecuteChanged;

			public bool CanExecute(object? parameter) => _viewModel.IsNotBusy;

			public async void Execute(object? parameter)
			{
				try
				{
					await _viewModel.OpenImageAsync(_viewModel.CancellationToken);
				}
				catch
				{
				}
			}

			public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}

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
					await _viewModel.AddImageAsync(_viewModel.CancellationToken);
				}
				catch
				{
				}
			}

			public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}

		public sealed class RemoveImageCommand : ICommand
		{
			private readonly ImagesViewModel _viewModel;

			public RemoveImageCommand(ImagesViewModel viewModel) => _viewModel = viewModel;

			public event EventHandler? CanExecuteChanged;

			public bool CanExecute(object? parameter) => _viewModel.CanRemoveImage;

			public async void Execute(object? parameter)
			{
				try
				{
					await _viewModel.RemoveImageAsync(_viewModel.CancellationToken);
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

	public static string? TrySanitizeName(ReadOnlySpan<char> name)
	{
		if (name.Length == 0) return null;

		return string.Create
		(
			name.Length,
			name,
			static (span, name) =>
			{
				while (true)
				{
					int index = name.IndexOfAnyExcept(NameAllowedCharacters);
					if (index < 0)
					{
						name.CopyTo(span);
						return;
					}
					name[..index].CopyTo(span);
					span[index] = '_';
					name = name[(index + 1)..];
					span = span[(index + 1)..];
				}
			}
		);
	}

	private readonly ObservableCollection<ImageViewModel> _images;
	private readonly ReadOnlyObservableCollection<ImageViewModel> _readOnlyImages;
	private ImageViewModel? _selectedImage;
	private readonly Commands.OpenImageCommand _openImageCommand;
	private readonly Commands.AddImageCommand _addImageCommand;
	private readonly Commands.RemoveImageCommand _removeImageCommand;

	private bool _isReady;
	private string? _loadedImageName;
	private byte[]? _loadedImageData;

	private readonly IFileOpenDialog _fileOpenDialog;
	private readonly INotificationSystem _notificationSystem;
	private IImageService? _imageService;
	private readonly ILogger<ImagesViewModel> _logger;
	private CancellationTokenSource? _cancellationTokenSource;

	public ImagesViewModel(ILogger<ImagesViewModel> logger, IFileOpenDialog fileOpenDialog, INotificationSystem notificationSystem)
	{
		_images = new();
		_readOnlyImages = new(_images);
		_logger = logger;
		_fileOpenDialog = fileOpenDialog;
		_notificationSystem = notificationSystem;
		_openImageCommand = new(this);
		_addImageCommand = new(this);
		_removeImageCommand = new(this);
		_cancellationTokenSource = new();
	}

	public void Dispose()
	{
		OnConnectionReset();
	}

	public ReadOnlyObservableCollection<ImageViewModel> Images => _readOnlyImages;

	public ImageViewModel? SelectedImage
	{
		get => _selectedImage;
		set
		{
			if (value != _selectedImage)
			{
				var oldValue = _selectedImage;
				_selectedImage = value;
				if (value is null != oldValue is null) _removeImageCommand.NotifyCanExecuteChanged();
				NotifyPropertyChanged(ChangedProperty.SelectedImage);
			}
		}
	}

	public ICommand OpenImageCommand => _openImageCommand;
	public ICommand AddImageCommand => _addImageCommand;
	public ICommand RemoveImageCommand => _removeImageCommand;

	public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? throw new InvalidOperationException("Not initialized.");

	internal void OnConnected(IImageService imageService)
	{
		_imageService = imageService;
		_cancellationTokenSource = new();
		IsNotBusy = true;
	}

	internal void OnConnectionReset()
	{
		if (Interlocked.Exchange(ref _cancellationTokenSource, null) is { } cts)
		{
			cts.Cancel();
		}

		IsNotBusy = false;
		ClearImage();
		_imageService = null;
		_images.Clear();
	}

	internal void OnImageUpdate(WatchNotificationKind kind, ImageInformation information)
	{
		switch (kind)
		{
		case WatchNotificationKind.Enumeration:
		case WatchNotificationKind.Addition:
			_images.Add(new ImageViewModel(information));
			break;
		case WatchNotificationKind.Removal:
			int i;
			for (i = 0; i < _images.Count; i++)
			{
				if (string.Equals(information.ImageName, _images[i].Name, StringComparison.OrdinalIgnoreCase))
				{
					_images.RemoveAt(i);
					break;
				}
			}
			break;
		}
	}

	public bool IsNotBusy
	{
		get => _isReady;
		private set
		{
			bool couldAddImage = CanAddImage;
			if (SetValue(ref _isReady, value, ChangedProperty.IsNotBusy))
			{
				_openImageCommand.NotifyCanExecuteChanged();
				if (couldAddImage != CanAddImage) _addImageCommand.NotifyCanExecuteChanged();
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

	public byte[]? LoadedImageData
	{
		get => _loadedImageData;
		private set
		{
			if (value != _loadedImageData)
			{
				bool couldAddImage = CanAddImage;
				_loadedImageData = value;
				NotifyPropertyChanged(ChangedProperty.LoadedImageData);
				if (couldAddImage != CanAddImage) _addImageCommand.NotifyCanExecuteChanged();
			}
		}
	}

	private void SetImage(string name, byte[] data)
	{
		LoadedImageName = name;
		LoadedImageData = data;
	}

	private void ClearImage()
	{
		LoadedImageName = null;
		LoadedImageData = null;
	}

	private bool CanAddImage => _isReady && _loadedImageName is not null && IsNameValid(_loadedImageName) && _loadedImageData is not null;
	private bool CanRemoveImage => _selectedImage != null;

	private async Task OpenImageAsync(CancellationToken cancellationToken)
	{
		var file = await _fileOpenDialog.OpenAsync([".bmp", ".gif", ".png", ".jpg", ".webp",]);

		if (file is null) return;
		byte[]? data;
		using (var stream = await file.OpenForReadAsync())
		{
			long length = stream.Length;
			if (length <= 0)
			{
				data = null;
			}
			else
			{
				data = new byte[length];
				await stream.ReadExactlyAsync(data, cancellationToken);
			}
		}

		if (data is not null)
		{
			string? name = null;
			if (file.Path is { Length: > 0 } path)
			{
				name = Path.GetFileNameWithoutExtension(path);
				if (!IsNameValid(name)) name = TrySanitizeName(name);
			}
			if (name is null) name = "img_" + RandomNumberGenerator.GetHexString(8);
			SetImage(name, data);
		}
		else
		{
			ClearImage();
		}
	}

	private async Task AddImageAsync(CancellationToken cancellationToken)
	{
		if (_imageService is null || _loadedImageName is null || !IsNameValid(_loadedImageName) || _loadedImageData is null) return;
		IsNotBusy = false;
		string imageName = _loadedImageName;
		try
		{
			string sharedMemoryName = await _imageService.BeginAddImageAsync(imageName, (uint)_loadedImageData.Length, cancellationToken);
			try
			{
				using (var sharedMemory = SharedMemory.Open(sharedMemoryName, (uint)_loadedImageData.Length, MemoryMappedFileAccess.Write))
				using (var memoryManager = sharedMemory.CreateMemoryManager(MemoryMappedFileAccess.Write))
				{
					_loadedImageData.AsSpan().CopyTo(memoryManager.GetSpan());
				}
			}
			catch
			{
				await _imageService.CancelAddImageAsync(sharedMemoryName, cancellationToken);
				throw;
			}
			await _imageService.EndAddImageAsync(sharedMemoryName, cancellationToken);
			_loadedImageName = null;
			_loadedImageData = null;
			NotifyPropertyChanged(ChangedProperty.LoadedImageName);
			NotifyPropertyChanged(ChangedProperty.LoadedImageData);
			_addImageCommand.NotifyCanExecuteChanged();
		}
		catch (DuplicateNameException)
		{
			_logger.ImageDuplictateName(imageName);
			_notificationSystem.PublishError("Failed to add image.", $"The name \"{imageName}\" is already in use.");
		}
		catch (Exception ex)
		{
			_logger.ImageAddError(ex);
			_notificationSystem.PublishError(ex, $"Failed to add the image {_loadedImageName}.");
		}
		finally
		{
			IsNotBusy = true;
		}
	}

	private async Task RemoveImageAsync(CancellationToken cancellationToken)
	{
		if (_imageService is null || _selectedImage is null) return;
		IsNotBusy = false;
		try
		{
			await _imageService.RemoveImageAsync(_selectedImage.Id, cancellationToken);
		}
		finally
		{
			IsNotBusy = true;
		}
	}
}

internal sealed partial class ImageViewModel : ApplicableResettableBindableObject
{
	private readonly UInt128 _id;
	private string _name;
	private readonly string _fileName;
	private readonly ushort _width;
	private readonly ushort _height;
	private readonly Exo.Images.ImageFormat _format;
	private readonly bool _isAnimated;

	public override bool IsChanged => false;

	public ImageViewModel(ImageInformation information)
	{
		_id = information.ImageId;
		_name = information.ImageName;
		_fileName = information.FileName;
		_width = information.Width;
		_height = information.Height;
		_format = information.Format;
		_isAnimated = information.IsAnimated;
	}

	public UInt128 Id => _id;

	public string Name
	{
		get => _name;
		set => SetValue(ref _name, value);
	}

	public string FileName => _fileName;
	public ushort Width => _width;
	public ushort Height => _height;
	public Exo.Images.ImageFormat Format => _format;
	public bool IsAnimated => _isAnimated;

	protected override Task ApplyChangesAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
	protected override void Reset() => throw new NotImplementedException();

}
