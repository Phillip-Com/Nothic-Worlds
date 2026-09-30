namespace NothicWorlds.Core.Storage;

/// <summary>
/// A temporary folder that keeps copies of assets the open world no longer uses but its undo
/// history still does. For example, after Clear Map and a save, the old map image isn't in the
/// saved file any more, so undoing Clear Map reloads it from here.
/// </summary>
/// <remarks>
/// The folder is deleted on <see cref="Dispose"/> (best effort; it's in the system's temporary
/// folder, which is cleaned up eventually anyway).
/// </remarks>
public sealed class AssetStash : IDisposable
{
    private readonly string _folder;

    /// <param name="folder">Where to keep copies. Created when the first copy is made.</param>
    public AssetStash(string folder)
    {
        _folder = Path.GetFullPath(folder);
    }

    /// <summary>
    /// Copies an asset into the stash and returns a source that reads the copy.
    /// </summary>
    /// <param name="name">The asset's name, e.g. <c>assets/1a2b….png</c>.</param>
    /// <param name="source">Where to read it from now.</param>
    /// <exception cref="IOException">The copy couldn't be made.</exception>
    /// <exception cref="UnauthorizedAccessException">The copy couldn't be made.</exception>
    public FileAssetSource Keep(string name, IAssetSource source)
    {
        Directory.CreateDirectory(_folder);

        // Asset names are generated ("assets/<32 hex>.<ext>"), so the file name is enough.
        string target = Path.Combine(_folder, Path.GetFileName(name));
        using (Stream input = source.OpenRead())
        using (var output = new FileStream(target, FileMode.Create, FileAccess.Write))
        {
            input.CopyTo(output);
        }

        return new FileAssetSource(target);
    }

    /// <summary>Deletes the stash folder and everything in it.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Best effort: the system cleans up its temporary folder eventually.
        }
    }
}
