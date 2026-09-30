namespace NothicWorlds.Core.Storage;

/// <summary>
/// Where an asset's bytes (e.g. a map image) can be read from: a file the user just imported,
/// or an entry inside a saved world file. Assets are streamed when saving, so large images are
/// never held in memory twice.
/// </summary>
public interface IAssetSource
{
    /// <summary>Opens the asset for reading. The caller disposes the stream.</summary>
    Stream OpenRead();
}
