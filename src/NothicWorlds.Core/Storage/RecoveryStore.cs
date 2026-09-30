using System.Text.Json;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Storage;

/// <summary>
/// Keeps recovery copies of worlds with unsaved changes, so work survives a crash (VISION.md
/// SAV-02). Each copy is a normal <c>.nworld</c> file named after the world's ID, plus a small
/// <c>.json</c> note recording where the world was originally saved.
/// </summary>
/// <param name="folder">Where recovery copies live. It's created if missing.</param>
public sealed class RecoveryStore(string folder)
{
    private const string NoteExtension = ".json";

    /// <summary>The folder holding recovery copies.</summary>
    public string Folder { get; } = Path.GetFullPath(folder);

    /// <summary>Writes (or replaces) the recovery copy of a world.</summary>
    /// <param name="world">The world, with its unsaved changes.</param>
    /// <param name="assets">A source for every asset the world references.</param>
    /// <param name="originalPath">
    /// Where the world was saved before, or null if it never was.
    /// </param>
    /// <exception cref="WorldFileException">The copy couldn't be written.</exception>
    public void Save(
        World world, IReadOnlyDictionary<string, IAssetSource> assets, string? originalPath)
    {
        Directory.CreateDirectory(Folder);
        string worldPath = WorldPath(world.Id);
        WorldPackage.Save(worldPath, world, assets);
        TryDelete(worldPath + WorldPackage.BackupSuffix);  // Recovery copies don't need backups.

        var note = new RecoveryNote(originalPath, DateTimeOffset.UtcNow);
        string notePath = NotePath(world.Id);
        File.WriteAllText(notePath + ".tmp", JsonSerializer.Serialize(note));
        File.Move(notePath + ".tmp", notePath, overwrite: true);
    }

    /// <summary>Lists the recovery copies that exist, newest first.</summary>
    public IReadOnlyList<RecoveryEntry> Find()
    {
        if (!Directory.Exists(Folder))
        {
            return [];
        }

        var entries = new List<RecoveryEntry>();
        foreach (string path in Directory.GetFiles(Folder, "*" + WorldPackage.Extension))
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out Guid id))
            {
                continue;
            }

            RecoveryNote? note = ReadNote(NotePath(id));
            DateTimeOffset saved = note?.SavedUtc ?? File.GetLastWriteTimeUtc(path);
            entries.Add(new RecoveryEntry(id, path, note?.OriginalPath, saved));
        }

        return entries.OrderByDescending(entry => entry.SavedUtc).ToList();
    }

    /// <summary>Deletes a world's recovery copy, if there is one. Safe to call anytime.</summary>
    public void Delete(Guid worldId)
    {
        string worldPath = WorldPath(worldId);
        TryDelete(worldPath);
        TryDelete(worldPath + WorldPackage.BackupSuffix);
        TryDelete(NotePath(worldId));
    }

    private string WorldPath(Guid id) => Path.Combine(Folder, $"{id:N}{WorldPackage.Extension}");

    private string NotePath(Guid id) => Path.Combine(Folder, $"{id:N}{NoteExtension}");

    // A missing or damaged note isn't fatal: the world itself can still be recovered.
    private static RecoveryNote? ReadNote(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<RecoveryNote>(File.ReadAllText(path))
                : null;
        }
        catch (Exception error) when (error is JsonException or IOException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Best effort; a leftover recovery file is offered again later, never lost.
        }
    }

    private sealed record RecoveryNote(string? OriginalPath, DateTimeOffset SavedUtc);
}
