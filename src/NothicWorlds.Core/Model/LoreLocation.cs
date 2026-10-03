using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// Where a journal entry or timeline event takes place (VISION.md LORE-01, LORE-02, LORE-03): a
/// planet or moon, optionally a region on it, and optionally a pinned spot on its surface.
/// </summary>
/// <param name="BodyId">The body it's on.</param>
/// <param name="Pin">The spot on the body's surface, or null for no particular spot.</param>
/// <param name="RegionId">
/// The region on that body it's in (see <see cref="Region"/>), or null for none.
/// </param>
public sealed record LoreLocation(Guid BodyId, GeoCoordinate? Pin = null, Guid? RegionId = null);
