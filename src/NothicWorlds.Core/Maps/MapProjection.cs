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
    /// image's shape (about 66°N–66°S for 2:1). Beyond that, the caps use the fill color.
    /// </summary>
    Mercator = 1,

    /// <summary>Robinson: the classic school and wall atlas layout, with rounded sides.</summary>
    Robinson = 2,

    /// <summary>Winkel tripel: National Geographic's standard world map layout.</summary>
    WinkelTripel = 3,

    /// <summary>Mollweide: an equal-area oval.</summary>
    Mollweide = 4,

    /// <summary>Gall–Peters: an equal-area rectangle that reaches the poles.</summary>
    GallPeters = 5,

    /// <summary>
    /// Polar: one circle centered on the north pole, with the equator at its edge (azimuthal
    /// equidistant). The southern hemisphere isn't covered and uses the fill color.
    /// </summary>
    Polar = 6,

    /// <summary>
    /// Two hemispheres: the western hemisphere in a left circle and the eastern in a right
    /// circle, split at longitude 0° and 180° (azimuthal equidistant within each circle).
    /// </summary>
    TwoHemispheres = 7,
}
