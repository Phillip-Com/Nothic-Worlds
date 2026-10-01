namespace NothicWorlds.Core.Model;

/// <summary>A celestial body in a world, such as a planet.</summary>
public sealed class Body
{
    /// <summary>Stable identity, kept across saves.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The body's display name.</summary>
    public string Name { get; set; } = "Planet";

    /// <summary>What kind of body this is.</summary>
    public BodyKind Kind { get; init; } = BodyKind.Planet;

    /// <summary>What's drawn on the body's surface.</summary>
    public SurfaceSettings Surface { get; } = new();

    /// <summary>Returns an independent copy of this body.</summary>
    public Body Clone()
    {
        var copy = new Body { Id = Id, Name = Name, Kind = Kind };
        copy.Surface.RestoreFrom(Surface);
        return copy;
    }
}
