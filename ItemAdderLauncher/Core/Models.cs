namespace ItemAdderLauncher.Core;

public sealed class ReleaseAsset
{
    public required string Name { get; init; }
    public required string DownloadUrl { get; init; }
    public bool IsZip => Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
}

public sealed class ReleaseInfo
{
    public required string TagName { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<ReleaseAsset> Assets { get; init; }

    public ReleaseAsset? SelectModAsset()
    {
        foreach (string preferred in new[] { "ItemAdder.dll", "MonSupermarketMod.dll" })
        {
            ReleaseAsset? asset = Assets.FirstOrDefault(a =>
                string.Equals(a.Name, preferred, StringComparison.OrdinalIgnoreCase));
            if (asset != null)
            {
                return asset;
            }
        }

        return Assets.FirstOrDefault(a => a.IsZip);
    }
}

public sealed class InstalledMod
{
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public required string Version { get; init; }
    public Version? ParsedVersion { get; init; }
}
