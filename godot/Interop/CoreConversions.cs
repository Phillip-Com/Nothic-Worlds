using NothicWorlds.Core.Model;

namespace NothicWorlds.Interop;

/// <summary>
/// Converts Core library types to and from Godot types. Vectors use the same axes (see
/// <c>NothicWorlds.Core.Geometry.SphericalCoordinates</c>), so no remapping is needed.
/// </summary>
public static class CoreConversions
{
    /// <summary>Converts a Core (System.Numerics) vector to a Godot vector.</summary>
    public static Godot.Vector3 ToGodot(this System.Numerics.Vector3 vector)
    {
        return new Godot.Vector3(vector.X, vector.Y, vector.Z);
    }

    /// <summary>Converts a Core color to a Godot color.</summary>
    public static Godot.Color ToGodot(this RgbColor color)
    {
        return Godot.Color.Color8(color.R, color.G, color.B);
    }

    /// <summary>Converts a Godot color to a Core color (alpha is dropped).</summary>
    public static RgbColor ToRgbColor(this Godot.Color color)
    {
        return new RgbColor((byte)color.R8, (byte)color.G8, (byte)color.B8);
    }
}
