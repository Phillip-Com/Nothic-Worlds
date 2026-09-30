namespace NothicWorlds.Interop;

/// <summary>
/// Converts Core library types to Godot types. Both use the same axes (see
/// <c>NothicWorlds.Core.Geometry.SphericalCoordinates</c>), so no remapping is needed.
/// </summary>
public static class CoreConversions
{
    /// <summary>Converts a Core (System.Numerics) vector to a Godot vector.</summary>
    public static Godot.Vector3 ToGodot(this System.Numerics.Vector3 vector)
    {
        return new Godot.Vector3(vector.X, vector.Y, vector.Z);
    }
}
