namespace Exo.Service.Configuration;

[TypeId(0x09EA0A89, 0x677C, 0x4CEA, 0xA0, 0x53, 0x21, 0x64, 0x69, 0x90, 0x70, 0x5D)]
internal readonly struct ConfigurationVersionDetails
{
	public uint ConfigurationVersion { get; init; }
	public string? GitCommitId { get; init; }
}
