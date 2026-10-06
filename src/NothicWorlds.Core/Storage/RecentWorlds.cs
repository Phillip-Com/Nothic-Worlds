namespace NothicWorlds.Core.Storage;

/// <summary>
/// The list of world files opened or saved lately, newest first, for the start screen
/// (VISION.md UI-06). It belongs to the computer, not to any world.
/// </summary>
public static class RecentWorlds
{
    /// <summary>The most worlds the list keeps.</summary>
    public const int MaxCount = 8;

    /// <summary>
    /// The list with <paramref name="path"/> moved (or added) to the front, without duplicates,
    /// cut to <see cref="MaxCount"/>. Paths that differ only in letter case or in the form of
    /// their slashes count as the same file (as on Windows).
    /// </summary>
    /// <param name="recent">The list so far, newest first.</param>
    /// <param name="path">A world file just opened or saved.</param>
    /// <exception cref="ArgumentException">The path is empty.</exception>
    public static IReadOnlyList<string> Add(IEnumerable<string> recent, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A recent world needs a path.", nameof(path));
        }

        return [.. recent.Where(other => !IsSameFile(other, path))
            .Prepend(path)
            .Take(MaxCount)];
    }

    /// <summary>The list without <paramref name="path"/>.</summary>
    public static IReadOnlyList<string> Remove(IEnumerable<string> recent, string path) =>
        [.. recent.Where(other => !IsSameFile(other, path))];

    private static bool IsSameFile(string a, string b) => string.Equals(
        a.Replace('\\', '/'), b.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
}
