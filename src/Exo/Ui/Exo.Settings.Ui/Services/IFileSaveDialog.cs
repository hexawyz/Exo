using System.Collections.Immutable;

namespace Exo.Settings.Ui.Services;

public interface IFileSaveDialog
{
	Task<IPickedFile?> ChooseAsync(ImmutableArray<(string Description, string Extension)> fileTypes);
}
