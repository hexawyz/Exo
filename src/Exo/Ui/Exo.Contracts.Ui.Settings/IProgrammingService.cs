using System.Collections.Immutable;
using System.Runtime.Serialization;
using System.ServiceModel;
using Exo.Programming;

namespace Exo.Contracts.Ui.Settings;

[ServiceContract(Name = "Programming")]
public interface IProgrammingService
{
	[OperationContract(Name = "GetModules")]
	ValueTask<IEnumerable<ModuleDefinition>> GetModulesAsync(CancellationToken cancellationToken);
}

//public sealed class ModulesResponse
//{
//	[DataMember(Order = 1)]
//	public ImmutableArray<ModuleDefinition> Modules { get; init; }
//}
