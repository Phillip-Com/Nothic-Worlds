using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// Where a journal entry or timeline event takes place (VISION.md LORE-02, LORE-03): a planet
/// or moon, and optionally a pinned spot on its surface.
/// </summary>
/// <param name="BodyId">The body it's on.</param>
/// <param name="Pin">The spot on the body's surface, or null for the body as a whole.</param>
public sealed record LoreLocation(Guid BodyId, GeoCoordinate? Pin = null);
