using System.Collections.Immutable;

namespace Exo.Settings.Ui.Services;

public interface IFileOpenDialog
{
	Task<IPickedFile?> OpenAsync(ImmutableArray<string> extensions);
}
