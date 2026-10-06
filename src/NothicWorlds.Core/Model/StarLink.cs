namespace NothicWorlds.Core.Model;

/// <summary>
/// One line of a constellation (VISION.md REN-07): it joins two stars of the world's star field,
/// named by their <see cref="Simulation.StarField"/> ids. The order of the ends doesn't matter.
/// </summary>
/// <param name="From">One star's id.</param>
/// <param name="To">The other star's id.</param>
public readonly record struct StarLink(int From, int To)
{
    /// <summary>Whether this joins the same two stars as <paramref name="other"/>.</summary>
    public bool Joins(StarLink other) =>
        (From == other.From && To == other.To) || (From == other.To && To == other.From);
}
