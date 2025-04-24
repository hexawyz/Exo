using System.Collections.Immutable;

namespace Exo.Discovery;

public readonly struct DnsSdInstanceInformation(string fullName, string hostName, ushort portNumber, string domain, string serviceType, string instanceName, ImmutableArray<string> textAttributes, ImmutableArray<string> ipAddresses)
{
	public string FullName { get; } = fullName;
	public string HostName { get; } = hostName;
	public ushort PortNumber { get; } = portNumber;
	public string Domain { get; } = domain;
	public string ServiceType { get; } = serviceType;
	public string InstanceName { get; } = instanceName;
	public ImmutableArray<string> TextAttributes { get; } = textAttributes;
	public ImmutableArray<string> IpAddresses { get; } = ipAddresses;
}
