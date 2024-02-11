namespace Exo.Discovery;

public delegate ValueTask<TResult?> SimpleComponentFactory<TCreationContext, TResult>(TCreationContext context, CancellationToken cancellationToken)
	where TCreationContext : class, IComponentCreationContext;
