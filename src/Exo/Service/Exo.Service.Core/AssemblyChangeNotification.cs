namespace Exo.Service;

public readonly struct AssemblyChangeNotification(WatchNotificationKind notificationKind, string assemblyPath, MetadataArchiveCategories availableMetadataArchives)
{
	public WatchNotificationKind NotificationKind { get; } = notificationKind;
	public string AssemblyPath { get; } = assemblyPath;
	public MetadataArchiveCategories AvailableMetadataArchives { get; } = availableMetadataArchives;
	private readonly ushort _extensionLength = (ushort)Path.GetExtension(assemblyPath).Length;

	private static string GetCategorySuffix(MetadataArchiveCategory category)
		=> category switch
		{
			MetadataArchiveCategory.Strings => ".Strings.xoa",
			MetadataArchiveCategory.LightingEffects => ".LightingEffects.xoa",
			MetadataArchiveCategory.LightingZones => ".LightingZones.xoa",
			MetadataArchiveCategory.Sensors => ".Sensors.xoa",
			MetadataArchiveCategory.Coolers => ".Coolers.xoa",
			_ => throw new InvalidOperationException(),
		};

	public string GetArchivePath(MetadataArchiveCategory category)
		=> $"{AssemblyPath.AsSpan(0, AssemblyPath.Length - _extensionLength)}{GetCategorySuffix(category)}";
}
