using Exo.Configuration;

namespace Exo.Service;

internal static class ConfigurationMigrationService
{
	[TypeId(0x09EA0A89, 0x677C, 0x4CEA, 0xA0, 0x53, 0x21, 0x64, 0x69, 0x90, 0x70, 0x5D)]
	private readonly struct ConfigurationVersionDetails
	{
		public uint ConfigurationVersion { get; init; }
		public string? GitCommitId { get; init; }
	}

	public static async Task InitializeAsync(ConfigurationService configurationService, string? gitCommitId, CancellationToken cancellationToken)
	{
		var rootContainer = configurationService.GetRootContainer();

		var result = await rootContainer.ReadValueAsync<ConfigurationVersionDetails>(cancellationToken).ConfigureAwait(false);

		bool shouldResetAssemblyCaches = !result.Found || result.Value.GitCommitId is null || result.Value.GitCommitId != gitCommitId;
		if (shouldResetAssemblyCaches)
		{
			// TODO: This is a very naive implementation of the cleanup, but it will guarantee that assembly discovery stuff is cleared, as well as possible other caches.
			// The proper implementation would probably be to allow each service to provide its own migration paths from version to version with some kind of synchronization mechanism,
			// but that will be for another time. (The best way to implement this is not clear to me yet)
			await ResetAssemblyCachesAsync(configurationService).ConfigureAwait(false);
		}

		const int ConfigurationVersion = 1;

		bool hasConfigurationChanged = !result.Found || result.Value.ConfigurationVersion != ConfigurationVersion || result.Value.GitCommitId != gitCommitId;
		if (hasConfigurationChanged)
		{
			await rootContainer.WriteValueAsync(new ConfigurationVersionDetails { ConfigurationVersion = ConfigurationVersion, GitCommitId = gitCommitId }, cancellationToken).ConfigureAwait(false);
		}
	}

	public static async Task ResetAssemblyCachesAsync(ConfigurationService configurationService)
	{
		await configurationService
			.GetContainer(ConfigurationContainerNames.Discovery)
			.GetContainer(ConfigurationContainerNames.DiscoveryFactory, GuidNameSerializer.Instance)
			.DeleteAllContainersAsync()
			.ConfigureAwait(false);

		await configurationService
			.GetContainer(ConfigurationContainerNames.Assembly, AssemblyNameSerializer.Instance)
			.DeleteAllContainersAsync()
			.ConfigureAwait(false);
	}
}
