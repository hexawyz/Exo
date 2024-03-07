using System.Collections.Immutable;

namespace Exo.Discovery;

public sealed class RootComponentCreationResult : ComponentCreationResult<RootComponentKey, Component>
{
	public RootComponentCreationResult(RootComponentKey registrationKey, Component component)
		: this([registrationKey], component, null) { }

	public RootComponentCreationResult(RootComponentKey registrationKey, Component component, IAsyncDisposable? disposableResult)
		: this([registrationKey], component, disposableResult) { }

	public RootComponentCreationResult(ImmutableArray<RootComponentKey> registrationKeys, Component component)
		: base(registrationKeys, component, null) { }

	public RootComponentCreationResult(ImmutableArray<RootComponentKey> registrationKeys, Component component, IAsyncDisposable? disposableResult)
		: base(registrationKeys, component, disposableResult) { }
}
