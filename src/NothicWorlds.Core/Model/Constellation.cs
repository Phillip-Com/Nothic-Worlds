namespace NothicWorlds.Core.Model;

/// <summary>
/// A named pattern on the world's night sky (VISION.md REN-07; owner's choice: drawn by joining
/// stars of the world's seeded star field). Immutable; change one by replacing it.
/// </summary>
public sealed record Constellation
{
    /// <summary>The longest a name can be, in characters.</summary>
    public const int MaxNameLength = 100;

    /// <summary>The most lines one constellation can have.</summary>
    public const int MaxLines = 200;

    /// <summary>The most constellations a world can have.</summary>
    public const int MaxCount = 200;

    /// <summary>Stable identity.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The constellation's name ("The Wanderer", "Kestrel's Wing").</summary>
    public required string Name { get; init; }

    /// <summary>The lines joining its stars, in the order they were drawn.</summary>
    public IReadOnlyList<StarLink> Lines { get; init; } = [];

    /// <summary>
    /// What's wrong with this constellation on the sky from <paramref name="starSeed"/>, or
    /// null if nothing.
    /// </summary>
    public string? Problem(int starSeed)
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            return $"a constellation needs a name of up to {MaxNameLength} characters";
        }

        if (Lines is null || Lines.Count > MaxLines)
        {
            return $"a constellation can have up to {MaxLines} lines";
        }

        if (Lines.Any(line => line.From == line.To))
        {
            return "a constellation has a line from a star to itself";
        }

        if (Lines.Any(line => !Simulation.StarField.HasStar(starSeed, line.From)
            || !Simulation.StarField.HasStar(starSeed, line.To)))
        {
            return "a constellation joins stars that aren't on this sky";
        }

        return Lines.Where((line, i) => Lines.Take(i).Any(line.Joins)).Any()
            ? "a constellation has the same line twice"
            : null;
    }

    /// <summary>
    /// What's wrong with a world's constellations on the sky from <paramref name="starSeed"/>
    /// (any one's problem, too many, or two sharing an id), or null if nothing.
    /// </summary>
    public static string? Problem(IReadOnlyList<Constellation> constellations, int starSeed)
    {
        if (constellations.Count > MaxCount)
        {
            return $"a sky can have up to {MaxCount} constellations";
        }

        if (constellations.Select(c => c.Id).Distinct().Count() != constellations.Count)
        {
            return "two constellations share an id";
        }

        return constellations.Select(c => c.Problem(starSeed)).FirstOrDefault(p => p is not null);
    }

    /// <summary>Constellations are equal when every part matches, including the lines.</summary>
    public bool Equals(Constellation? other)
    {
        return other is not null
            && Id == other.Id
            && Name == other.Name
            && Lines.SequenceEqual(other.Lines);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Id, Name, Lines.Count);
}
