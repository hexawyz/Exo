using System.Runtime.InteropServices;
using Exo.Programming;
using Exo.Contracts.Ui.Settings;

namespace Exo.Service.Services;

internal class GrpcProgrammingService : IProgrammingService
{
	private readonly ProgrammingService _programmingService;

	public GrpcProgrammingService(ProgrammingService programmingService) => _programmingService = programmingService;

	public ValueTask<IEnumerable<ModuleDefinition>> GetModulesAsync(CancellationToken cancellationToken)
		=> new(ImmutableCollectionsMarshal.AsArray(_programmingService.GetModules())!);
}
