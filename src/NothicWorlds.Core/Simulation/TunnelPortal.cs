using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A tunnel's portal near a first-person eye (VISION.md BOD-13): its front and block
/// (<see cref="StoneTunnel.Portal"/>) in its own frame, and how far out to each side the
/// coarser ground around it is sunk out of sight (<see cref="StoneTunnel.Hides"/>).
/// </summary>
public sealed record TunnelPortal(BridgeShown Shown, double HiddenHalfWidthMeters)
{
    /// <summary>
    /// Whether a unit direction is where the coarser ground is sunk out of sight, so walkers
    /// don't stand on that ground.
    /// </summary>
    public bool Covers(Vector3D direction)
    {
        Vector3D along = Shown.End - Shown.Start;
        double lengthSquared = along.Dot(along);
        if (lengthSquared <= 0)
        {
            return false;
        }

        Vector3D offset = direction - Shown.Start;
        double x = offset.Dot(along) / lengthSquared * Shown.LengthMeters;
        double y = offset.Dot(Shown.Side) * Shown.RadiusMeters;
        return x >= -StoneTunnel.HiddenInFrontMeters && x <= StoneTunnel.HiddenBehindMeters
            && Math.Abs(y) <= HiddenHalfWidthMeters;
    }
}
