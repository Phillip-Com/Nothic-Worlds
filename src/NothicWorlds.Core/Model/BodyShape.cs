namespace NothicWorlds.Core.Model;

/// <summary>
/// The shape of a planet or moon (VISION.md BOD-02). Stars and comets are spheres.
/// </summary>
public enum BodyShape
{
    /// <summary>An ordinary globe.</summary>
    Sphere,

    /// <summary>
    /// A flat world: a disc with the whole map on its top face, the north pole at the center
    /// and the far south around the rim (see <see cref="Geometry.FlatDisc"/>).
    /// </summary>
    FlatDisc,
}
