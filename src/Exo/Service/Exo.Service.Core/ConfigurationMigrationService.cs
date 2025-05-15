using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Exo.Configuration;
using Exo.Service.Configuration;

namespace Exo.Service;

internal static class ConfigurationMigrationService
{
	public static async Task InitializeAsync(ConfigurationService configurationService, ImmutableArray<byte> gitCommitId, CancellationToken cancellationToken)
	{
		var rootContainer = configurationService.GetRootContainer();

		var result = await rootContainer.ReadValueAsync(SourceGenerationContext.Default.ConfigurationVersionDetails, cancellationToken).ConfigureAwait(false);

		string? commitIdString = !gitCommitId.IsDefaultOrEmpty ? Convert.ToHexString(ImmutableCollectionsMarshal.AsArray(gitCommitId)!) : null;

		bool shouldResetAssemblyCaches = !result.Found || result.Value.GitCommitId is null || result.Value.GitCommitId != commitIdString;
		if (shouldResetAssemblyCaches)
		{
			// TODO: This is a very naive implementation of the cleanup, but it will guarantee that assembly discovery stuff is cleared, as well as possible other caches.
			// The proper implementation would probably be to allow each service to provide its own migration paths from version to version with some kind of synchronization mechanism,
			// but that will be for another time. (The best way to implement this is not clear to me yet)
			await ResetAssemblyCachesAsync(configurationService).ConfigureAwait(false);
		}

		const int ConfigurationVersion = 1;

		bool hasConfigurationChanged = !result.Found || result.Value.ConfigurationVersion != ConfigurationVersion || result.Value.GitCommitId != commitIdString;
		if (hasConfigurationChanged)
		{
			await rootContainer.WriteValueAsync
			(
				new ConfigurationVersionDetails { ConfigurationVersion = ConfigurationVersion, GitCommitId = commitIdString },
				SourceGenerationContext.Default.ConfigurationVersionDetails,
				cancellationToken
			).ConfigureAwait(false);
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
