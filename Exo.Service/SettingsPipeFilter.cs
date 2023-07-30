using System;
using System.Net;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;

namespace Exo.Service;

// This filter ensures that requests are coming from a specific pipe endpoint.
// For security reasons, we do not want to provide access to some services on unsecured endpoints.
internal sealed class SettingsPipeFilter : IEndpointFilter
{
	// For now we use reflection to retrieve the LocalEndPoint information.
	// We should be able to rely on the IConnectionEndPointFeature to access this information, but it is not yet available.
	private static readonly ConditionalWeakTable<Type, Func<object, EndPoint?>> EndpointGetterCache = new();

	private Func<object, EndPoint?> GetEndpointGetter(Type type)
		=> EndpointGetterCache.GetValue(type, GetNonCachedEndpointGetter);

	private Func<object, EndPoint?> GetNonCachedEndpointGetter(Type type)
	{
		if (type.GetProperty("LocalEndPoint") is { } property)
		{
			var m = new DynamicMethod("GetLocalEndPoint", typeof(EndPoint), new[] { typeof(object) }, true);
			var ilGenerator = m.GetILGenerator();
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.EmitCall(OpCodes.Callvirt, property.GetMethod!, null);
			ilGenerator.Emit(OpCodes.Ret);
			return (Func<object, EndPoint?>)m.CreateDelegate(typeof(Func<object, EndPoint?>));
		}
		else
		{
			return _ => null;
		}
	}

	public string PipeName { get; }

	public SettingsPipeFilter(string pipeName) => PipeName = pipeName;

	public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
	{
		if (context.HttpContext.Features.Get<IConnectionNamedPipeFeature>() is IConnectionNamedPipeFeature feature)
		{
			if (GetEndpointGetter(feature.GetType())(feature) is NamedPipeEndPoint endPoint && endPoint.PipeName == PipeName)
			{
				return next(context);
			}
		}

		return ValueTask.FromResult<object?>(Results.NotFound());
	}
}
