using DeviceTools;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 1001, EventName = "DnsSdDiscoveryStarting", Level = LogLevel.Debug, Message = "DNS-SD device discovery is starting…")]
	public static partial void DnsSdDiscoveryStarting(this ILogger logger);

	[LoggerMessage(EventId = 1002, EventName = "DnsSdDiscoveryStarted", Level = LogLevel.Debug, Message = "DNS-SD device discovery has started.")]
	public static partial void DnsSdDiscoveryStarted(this ILogger logger);

	[LoggerMessage(EventId = 1003, EventName = "DnsSdInstanceArrival", Level = LogLevel.Debug, Message = "Arrival of DNS-SD instance: \"{InstanceName}\".")]
	public static partial void DnsSdInstanceArrival(this ILogger logger, string instanceName);

	[LoggerMessage(EventId = 1004, EventName = "DnsSdInstanceRemoval", Level = LogLevel.Debug, Message = "Removal of DNS-SD instance: \"{InstanceName}\".")]
	public static partial void DnsSdInstanceRemoval(this ILogger logger, string instanceName);

	[LoggerMessage(EventId = 1005,
		EventName = "DnsSdFactoryMissingKeys",
		Level = LogLevel.Error,
		Message = "The factory did not define any valid keys for DNS-SD discovery.")]
	public static partial void DnsSdFactoryMissingKeys(this ILogger logger);

	[LoggerMessage(EventId = 1006,
		EventName = "DnsSdServiceTypeDuplicateKey",
		Level = LogLevel.Error,
		Message = "The factory defines more than one key for {ServiceType}.")]
	public static partial void DnsSdServiceTypeDuplicateKey
	(
		this ILogger logger,
		string serviceType
	);

	[LoggerMessage(EventId = 1007,
		EventName = "DnsSdServiceTypeRegistrationConflict",
		Level = LogLevel.Error,
		Message = "A factory for service type {ServiceType} was already registered.")]
	public static partial void DnsSdServiceTypeRegistrationConflict
	(
		this ILogger logger,
		string serviceType
	);
}
