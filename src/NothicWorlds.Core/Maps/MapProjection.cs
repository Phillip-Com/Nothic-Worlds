namespace NothicWorlds.Core.Maps;

/// <summary>
/// How a flat map image is laid out, which decides how it wraps onto the globe
/// (VISION.md MAP-01, MAP-03). The numeric values are shared with <c>planet.gdshader</c>.
/// </summary>
public enum MapProjection
{
    /// <summary>
    /// Shown to the user as "Globe map". Latitude and longitude form a straight grid across a 2:1
    /// image covering the whole globe. This is right for maps made for globes, but flat-drawn maps
    /// look pinched toward the poles.
    /// </summary>
    Equirectangular = 0,

    /// <summary>
    /// Shown to the user as "Flat map". It keeps shapes as drawn, which suits hand-drawn and
    /// fantasy-tool maps. It can't reach the poles, so the latitude it covers depends on the
    /// image's shape (about 66°N–66°S for 2:1). Beyond that, the map's edge is stretched to the
    /// pole.
    /// </summary>
    Mercator = 1,
}
