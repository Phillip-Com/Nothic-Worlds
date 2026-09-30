namespace NothicWorlds.Core.Model;

/// <summary>
/// A user's world: everything that gets saved to a <c>.nworld</c> file (see
/// docs/world-format.md). This is the in-memory form; <c>Storage.WorldPackage</c> reads and
/// writes it.
/// </summary>
public sealed class World
{
    /// <summary>
    /// Stable identity, kept across saves (e.g. for recovery copies, or future sharing).
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The world's display name.</summary>
    public string Name { get; set; } = "Untitled World";

    /// <summary>When the world was first created.</summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>When the world was last saved.</summary>
    public DateTimeOffset ModifiedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The world's celestial bodies. Currently always one planet. It's a list so suns, moons,
    /// and more can be added later without changing the file layout.
    /// </summary>
    public List<Body> Bodies { get; } = [];

    /// <summary>
    /// Where the camera was looking when the world was saved, or null for the default.
    /// </summary>
    public CameraView? View { get; set; }

    /// <summary>Creates a new world with a single, unmapped planet.</summary>
    public static World CreateNew(string name = "Untitled World")
    {
        var world = new World { Name = name };
        world.Bodies.Add(new Body { Name = "Planet", Kind = BodyKind.Planet });
        return world;
    }
}
