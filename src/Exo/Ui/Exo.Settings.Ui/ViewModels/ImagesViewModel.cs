using System.Collections.ObjectModel;
using System.Windows.Input;
using Exo.Ui;
using Microsoft.UI.Xaml.Media;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class ImagesViewModel : BindableObject
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

	private readonly ObservableCollection<ImageViewModel> _images;
	private readonly ReadOnlyObservableCollection<ImageViewModel> _readOnlyImages;
	private readonly Commands.AddImageCommand _addImageCommand;

	private string? _loadedImageName;
	private byte[]? _loadedImageData;

	public ImagesViewModel()
	{
		_images = new();
		_readOnlyImages = new(_images);
		_addImageCommand = new(this);
	}

	public ReadOnlyObservableCollection<ImageViewModel> Images => _readOnlyImages;
	public ICommand AddImageCommand => _addImageCommand;

	public string? LoadedImageName
	{
		get => _loadedImageName;
		private set => SetValue(ref _loadedImageName, value);
	}

	public byte[]? LoadedImageData
	{
		get => _loadedImageData;
		private set => SetValue(ref _loadedImageData, value);
	}

	public void SetImage(string name, byte[] data)
	{
		LoadedImageName = name;
		LoadedImageData = data;
	}

	public void ClearImage()
	{
		LoadedImageName = null;
		LoadedImageData = null;
	}

	// TODO: Remove. This is for testing the UI without backend.
	public void TestAddImageToList(string fileName)
	{
		_images.Add(new(Path.GetFileNameWithoutExtension(fileName), fileName));
	}

	private bool CanAddImage => _loadedImageData is not null;

	private async Task AddImageAsync(CancellationToken cancellationToken)
	{
	}
}

internal sealed partial class ImageViewModel : ApplicableResettableBindableObject
{
	private string _name;
	private readonly string _fileName;

	public override bool IsChanged => false;

	public ImageViewModel(string name, string fileName)
	{
		_name = name;
		_fileName = fileName;
	}

	public string Name
	{
		get => _name;
		set => SetValue(ref _name, value);
	}

	public string FileName => _fileName;

	protected override Task ApplyChangesAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
	protected override void Reset() => throw new NotImplementedException();

}
