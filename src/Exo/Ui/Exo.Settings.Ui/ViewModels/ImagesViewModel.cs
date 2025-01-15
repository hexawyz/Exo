using System.Collections.ObjectModel;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class ImagesViewModel : BindableObject
{
	private readonly ObservableCollection<ImageViewModel> _images;
	private readonly ReadOnlyObservableCollection<ImageViewModel> _readOnlyImages;

	public ImagesViewModel()
	{
		_images = new();
		_readOnlyImages = new(_images);
	}

	public ReadOnlyObservableCollection<ImageViewModel> Images => _readOnlyImages;

	protected void UploadImage() { }
}

internal sealed partial class ImageViewModel : ApplicableResettableBindableObject
{
	private string _name;

	public override bool IsChanged => false;

	public ImageViewModel(string name)
	{
		_name = name;
	}

	public string Name
	{
		get => _name;
		set => SetValue(ref _name, value);
	}

	protected override Task ApplyChangesAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
	protected override void Reset() => throw new NotImplementedException();

}
