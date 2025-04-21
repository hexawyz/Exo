using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

internal static partial class LoggerExtensions
{
	[LoggerMessage(Level = LogLevel.Debug, Message = "DNS-SD device discovery is starting…")]
	public static partial void DnsSdDiscoveryStarting(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Debug, Message = "DNS-SD device discovery has started.")]
	public static partial void DnsSdDiscoveryStarted(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Arrival of DNS-SD instance: \"{InstanceName}\".")]
	public static partial void DnsSdInstanceArrival(this ILogger logger, string instanceName);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Update of DNS-SD instance: \"{InstanceName}\".")]
	public static partial void DnsSdInstanceUpdate(this ILogger logger, string instanceName);

	[LoggerMessage(Level = LogLevel.Debug, Message = "Removal of DNS-SD instance: \"{InstanceName}\".")]
	public static partial void DnsSdInstanceRemoval(this ILogger logger, string instanceName);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory did not define any valid keys for DNS-SD discovery.")]
	public static partial void DnsSdFactoryMissingKeys(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Error, Message = "The factory defines more than one key for {ServiceType}.")]
	public static partial void DnsSdServiceTypeDuplicateKey
	(
		this ILogger logger,
		string serviceType
	);

	[LoggerMessage(Level = LogLevel.Error, Message = "A factory for service type {ServiceType} was already registered.")]
	public static partial void DnsSdServiceTypeRegistrationConflict
	(
		this ILogger logger,
		string serviceType
	);
}
