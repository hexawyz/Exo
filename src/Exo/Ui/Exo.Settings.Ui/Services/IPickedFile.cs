namespace Exo.Settings.Ui.Services;

public interface IPickedFile
{
	string? Path { get; }
	Task<Stream> OpenForReadAsync();
}
