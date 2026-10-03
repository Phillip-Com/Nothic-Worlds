using NothicWorlds.Core.Geometry;

namespace NothicWorlds.UI;

/// <summary>Words for places on a body's surface (VISION.md LORE-02).</summary>
public static class PlaceText
{
    /// <summary>A pinned spot, e.g. "42.5° N, 71° W".</summary>
    public static string Describe(GeoCoordinate pin)
    {
        string northSouth = pin.LatitudeDegrees >= 0 ? "N" : "S";
        string eastWest = pin.LongitudeDegrees >= 0 ? "E" : "W";
        return $"{Math.Abs(pin.LatitudeDegrees):0.##}° {northSouth}, " +
            $"{Math.Abs(pin.LongitudeDegrees):0.##}° {eastWest}";
    }
}
