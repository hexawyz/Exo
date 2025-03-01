using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

/// <summary>The context available for DNS-SD device driver creation.</summary>
/// <remarks>
/// <para>All of the properties below can optionally be accessed to retrieve information on the device being initialized.</para>
/// <para>
/// This provides very basic information for regarding DNS-SD devices.
/// This should generally be enough for drivers to do their own thing.
/// </para>
/// </remarks>
public sealed class DnsSdDriverCreationContext : DriverCreationContext
{
	private readonly DnsSdDiscoverySubsystem _discoverySubsystem;
	/// <summary>Gets the keys corresponding to all devices and devices interfaces that are in the container.</summary>
	/// <remarks>Most driver factories can return these keys as-is, without the need to recompute it themselves.</remarks>
	public ImmutableArray<DnsSdInstanceId> Keys { get; }

	private readonly DnsSdInstanceInformation _instanceInformation;

	/// <summary>Gets the full name of the found instance.</summary>
	public string FullName => _instanceInformation.FullName;
	/// <summary>Gets the DNS host name of the found instance.</summary>
	public string HostName => _instanceInformation.HostName;
	/// <summary>Gets the port number of the found instance.</summary>
	public ushort PortNumber => _instanceInformation.PortNumber;
	/// <summary>Gets the domain in which this instance was found (generally local).</summary>
	public string DomainName => _instanceInformation.Domain;
	/// <summary>Gets the service type of the found instance.</summary>
	public string ServiceType => _instanceInformation.ServiceType;
	/// <summary>Gets the instance name of the found instance.</summary>
	/// <remarks>This should be usable as the friendly name.</remarks>
	public string InstanceName => _instanceInformation.InstanceName;
	/// <summary></summary>
	public ImmutableArray<string> TextAttributes => _instanceInformation.TextAttributes;

	protected override INestedDriverRegistryProvider NestedDriverRegistryProvider => _discoverySubsystem.DriverRegistry;
	public override ILoggerFactory LoggerFactory => _discoverySubsystem.LoggerFactory;

	private readonly DnsSdDeviceLifetime _lifetime;
	internal DnsSdDeviceLifetime Lifetime => _lifetime;

	internal DnsSdDriverCreationContext
	(
		DnsSdDiscoverySubsystem discoverySubsystem,
		ImmutableArray<DnsSdInstanceId> keys,
		DnsSdInstanceInformation instanceInformation
	)
	{
		_discoverySubsystem = discoverySubsystem;
		Keys = keys;
		_instanceInformation = instanceInformation;
		_lifetime = _discoverySubsystem.CreateLifetime(keys[0].ToString());
	}

	protected override void CollectDisposableDependencies(ref DisposableDependencyBuilder builder)
	{
		base.CollectDisposableDependencies(ref builder);
		builder.Add(_lifetime);
	}
}
