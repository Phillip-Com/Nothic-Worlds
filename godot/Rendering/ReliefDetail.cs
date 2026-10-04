namespace NothicWorlds.Rendering;

/// <summary>
/// How finely sculpted globes' shapes are drawn (VISION.md BOD-04, REN-03; owner's choice: a
/// quality setting): squares along each edge of the mesh's six faces. Slopes are shaded at
/// every height cell whatever the detail; finer detail only sharpens small features' outline
/// against the sky, and costs more to draw.
/// </summary>
public enum ReliefDetail
{
    /// <summary>64 squares a face (about 16 cells each): the lightest.</summary>
    Low = 64,

    /// <summary>96 squares a face (about 11 cells each): the default.</summary>
    Standard = 96,

    /// <summary>128 squares a face (8 cells each): the sharpest outline.</summary>
    High = 128,
}
