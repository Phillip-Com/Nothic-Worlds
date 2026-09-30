namespace NothicWorlds.Core.Storage;

/// <summary>
/// An asset read from a file on disk, such as a map image the user just imported.
/// </summary>
/// <param name="Path">Full path of the file.</param>
public sealed record FileAssetSource(string Path) : IAssetSource
{
    /// <inheritdoc/>
    public Stream OpenRead()
    {
        return File.OpenRead(Path);
    }
}
