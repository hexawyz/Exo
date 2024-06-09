using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;

namespace Exo.Service.Grpc;

internal sealed class GrpcMetadataService : IMetadataService
{
	private readonly string _mainStringsArchivePath;
	private readonly string _mainLightingEffectsArchivePath;
	private readonly IMetadataSourceProvider _metadataSourceProvider;

	public GrpcMetadataService(string mainAssemblyPath, IMetadataSourceProvider metadataSourceProvider)
	{
		_mainStringsArchivePath = $"{mainAssemblyPath.AsSpan(0, mainAssemblyPath.Length - Path.GetExtension(mainAssemblyPath.AsSpan()).Length)}.Strings.xoa";
		_mainLightingEffectsArchivePath = $"{mainAssemblyPath.AsSpan(0, mainAssemblyPath.Length - Path.GetExtension(mainAssemblyPath.AsSpan()).Length)}.LightingEffects.xoa";
		_metadataSourceProvider = metadataSourceProvider;
	}

	public ValueTask<string> GetMainStringArchivePathAsync(CancellationToken cancellationToken) => new(_mainStringsArchivePath);

	public ValueTask<string> GetMainLightingEffectArchivePathAsync(CancellationToken cancellationToken) => new(_mainLightingEffectsArchivePath);

	public async IAsyncEnumerable<Contracts.Ui.Settings.MetadataSourceChangeNotification> WatchMetadataSourceChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _metadataSourceProvider.WatchMetadataSourceChangesAsync(cancellationToken).ConfigureAwait(false))
		{
			yield return new Contracts.Ui.Settings.MetadataSourceChangeNotification
			{
				NotificationKind = notification.NotificationKind.ToGrpc(),
				Category = notification.Category.ToGrpc(),
				ArchivePath = notification.ArchivePath,
			};
		}
	}
}
