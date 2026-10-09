namespace NothicWorlds.Rendering;

/// <summary>
/// How finely the ground is drawn around the eye while standing (File ▸ Settings; VISION.md
/// REN-06, owner's choice): how many squares across each ground tile (GroundTiles). The tiles
/// are laid out the same at every detail, so it sets the triangles drawn, not the draw calls.
/// </summary>
public enum StandingGroundDetail
{
    /// <summary>Tiles 8 squares across (8 m underfoot): the lightest.</summary>
    Low,

    /// <summary>Tiles 16 squares across (4 m underfoot): the default.</summary>
    Standard,

    /// <summary>Tiles 32 squares across (2 m underfoot): the sharpest.</summary>
    High,
}
