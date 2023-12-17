using Exo.Programming;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class ModuleViewModel
{
	private readonly ModuleDefinition _definition;

	public ModuleViewModel(ModuleDefinition definition) => _definition = definition;

	public string Name => _definition.Name;
	public string Comment => _definition.Comment;
}
