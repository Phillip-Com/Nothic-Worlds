using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Storage;

/// <summary>
/// Reads and writes <c>.nworld</c> files (docs/world-format.md): a zip holding
/// <c>world.json</c> and the world's assets (the user's original map images, unchanged).
/// </summary>
/// <remarks>
/// Saving never damages an existing world. The new file is written and read back in full next
/// to the target, and only then swapped in, with the previous version kept as
/// <c>&lt;name&gt;.nworld.bak</c> (CLAUDE.md §4: saves must be atomic).
/// </remarks>
public static class WorldPackage
{
    /// <summary>File extension for world files.</summary>
    public const string Extension = ".nworld";

    /// <summary>Suffix of the backup of the previous save.</summary>
    public const string BackupSuffix = ".bak";

    // The in-progress file while saving; a leftover one (from a crash) is safe to delete.
    private const string SavingSuffix = ".saving";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    /// <summary>
    /// Creates a new, unique name for an imported image asset, e.g. <c>assets/1a2b….png</c>.
    /// </summary>
    /// <param name="fileExtension">The image's extension, e.g. ".png".</param>
    /// <exception cref="ArgumentException">The extension isn't a supported image type.</exception>
    public static string CreateAssetName(string fileExtension)
    {
        return WorldFormat.NewAssetName(fileExtension);
    }

    /// <summary>
    /// Saves a world to <paramref name="path"/>. Assets the world no longer references are
    /// left out. If anything fails, any existing file at the path is left untouched.
    /// </summary>
    /// <param name="path">Where to save, normally ending in <see cref="Extension"/>.</param>
    /// <param name="world">The world to save.</param>
    /// <param name="assets">A source for every asset the world references, by name.</param>
    /// <exception cref="WorldFileException">The world couldn't be saved.</exception>
    public static void Save(
        string path, World world, IReadOnlyDictionary<string, IAssetSource> assets)
    {
        string fullPath = Path.GetFullPath(path);
        List<string> assetNames = ReferencedAssets(world);
        foreach (string name in assetNames.Where(name => !assets.ContainsKey(name)))
        {
            throw new WorldFileException($"Couldn't save: the map image '{name}' is missing.");
        }

        string tempPath = fullPath + SavingSuffix;
        try
        {
            TryDelete(tempPath);
            WriteArchive(tempPath, WorldMapper.ToDocument(world), assetNames, assets);
            Load(tempPath);  // Proves the new file reads back before it replaces anything.
            Commit(tempPath, fullPath);
        }
        catch (Exception error)
        {
            TryDelete(tempPath);
            if (error is WorldFileException)
            {
                throw;
            }

            throw new WorldFileException($"Couldn't save the world: {error.Message}", error);
        }
    }

    /// <summary>Loads a world file, upgrading older format versions.</summary>
    /// <exception cref="WorldFileException">
    /// The file is missing, unreadable, damaged, or from a newer version of the app.
    /// </exception>
    public static LoadedWorld Load(string path)
    {
        string fullPath = Path.GetFullPath(path);
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(fullPath);
            World world = WorldMapper.ToWorld(ReadDocument(archive));

            var assets = new Dictionary<string, IAssetSource>();
            foreach (string name in ReferencedAssets(world))
            {
                ZipArchiveEntry entry = archive.GetEntry(name) ?? throw new WorldFileException(
                    $"The world file is damaged: its map image '{name}' is missing.");
                if (entry.Length > MapImageRules.MaxFileBytes)
                {
                    throw new WorldFileException(
                        $"The world file is damaged: the map image '{name}' is too large.");
                }

                assets[name] = new PackageAssetSource(fullPath, name);
            }

            return new LoadedWorld(world, assets);
        }
        catch (WorldFileException)
        {
            throw;
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new WorldFileException("The world file doesn't exist.", error);
        }
        catch (Exception error) when (error is InvalidDataException)
        {
            throw new WorldFileException(
                "This file is damaged or isn't a Nothic Worlds world file.", error);
        }
        catch (JsonException error)
        {
            throw new WorldFileException($"The world data is damaged: {error.Message}", error);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new WorldFileException(
                $"The world file couldn't be read: {error.Message}", error);
        }
    }

    private static WorldDocument ReadDocument(ZipArchive archive)
    {
        ZipArchiveEntry entry = archive.GetEntry(WorldFormat.DocumentEntryName)
            ?? throw new WorldFileException(
                "This isn't a Nothic Worlds world file (it has no world data).");
        if (entry.Length > WorldFormat.MaxDocumentBytes)
        {
            throw new WorldFileException("The world file is damaged: its world data is too large.");
        }

        JsonNode? node;
        using (Stream stream = entry.Open())
        {
            node = JsonNode.Parse(stream);
        }

        JsonObject document = node as JsonObject
            ?? throw new WorldFileException("The world data is damaged: it isn't a JSON object.");
        return WorldFormat.Upgrade(document).Deserialize<WorldDocument>(_jsonOptions)
            ?? throw new WorldFileException("The world data is damaged: it's empty.");
    }

    private static void WriteArchive(
        string path,
        WorldDocument document,
        List<string> assetNames,
        IReadOnlyDictionary<string, IAssetSource> assets)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite);
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry documentEntry =
                archive.CreateEntry(WorldFormat.DocumentEntryName, CompressionLevel.Optimal);
            using (Stream stream = documentEntry.Open())
            {
                JsonSerializer.Serialize(stream, document, _jsonOptions);
            }

            // Images are already compressed, so they're stored as-is (much faster to save).
            foreach (string name in assetNames)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
                using Stream source = assets[name].OpenRead();
                using Stream target = entry.Open();
                source.CopyTo(target);
            }
        }

        // Make sure the bytes are really on disk before the file replaces the previous save.
        file.Flush(flushToDisk: true);
    }

    private static void Commit(string tempPath, string fullPath)
    {
        if (File.Exists(fullPath))
        {
            File.Replace(tempPath, fullPath, fullPath + BackupSuffix, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, fullPath);
        }
    }

    private static List<string> ReferencedAssets(World world)
    {
        return world.Bodies
            .Select(body => body.Surface.Map?.AssetName)
            .OfType<string>()
            .Distinct()
            .ToList();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover temp file is harmless and replaced on the next save.
        }
    }
}
