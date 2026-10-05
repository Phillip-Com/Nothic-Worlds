namespace NothicWorlds.Core.Model;

/// <summary>
/// Where a journal entry's box sits on a lore diagram (VISION.md LORE-04), in the diagram's
/// own units (about a pixel at normal zoom), measured to the box's middle.
/// </summary>
/// <param name="EntryId">The entry shown.</param>
/// <param name="X">Across, rightward.</param>
/// <param name="Y">Down.</param>
public sealed record DiagramPlacement(Guid EntryId, double X, double Y);
