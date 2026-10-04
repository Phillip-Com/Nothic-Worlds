using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// How heavy bodies are (VISION.md SIM-04): a body's density, as set or typical for its kind and
/// size, times the volume of a globe its size. A flat world weighs as much as a globe of its
/// radius, and a world tree as if its whole size were wood (most of it is air, but the guide
/// only needs a rough figure, and the user can set it).
/// </summary>
public static class BodyMass
{
    /// <summary>The Sun's mass, in kg.</summary>
    public const double SunKg = 1.989e30;

    /// <summary>The Earth's mass, in kg.</summary>
    public const double EarthKg = 5.972e24;

    private const double SunRadiusKm = 695_700;
    private const double EarthRadiusKm = 6371;

    // Rocky worlds up to 1.5 Earth radii are as dense as Earth; from 4 Earth radii up they're
    // gas giants as dense as Jupiter; in between the density slides from one to the other.
    private const double RockyDensity = 5.51;
    private const double GasGiantDensity = 1.33;
    private const double LargestRockyRadii = 1.5;
    private const double SmallestGiantRadii = 4;

    private const double MoonDensity = 3.3;  // Like the Moon, between rock and ice
    private const double CometDensity = 0.6;  // A loose snowball of ice and dust
    private const double TreeDensity = 0.5;  // Wood

    // Stars are given a main-sequence mass for their size (radius grows as mass^0.8), kept
    // within the range real stars have.
    private const double MainSequenceExponent = 0.8;
    private const double LightestStarSuns = 0.08;
    private const double HeaviestStarSuns = 100;

    /// <summary>The body's density in g/cm³: as set, or <see cref="TypicalDensity"/>.</summary>
    public static double Density(Body body) =>
        body.DensityGramsPerCm3 ?? TypicalDensity(body.Kind, body.RadiusKm);

    /// <summary>The body's mass, in kg.</summary>
    public static double Kg(Body body) => Density(body) * 1000 * GlobeVolumeM3(body.RadiusKm);

    /// <summary>
    /// The usual density, in g/cm³, of a body of this kind and radius: Earth-like rock for small
    /// planets sliding to Jupiter-like gas for big ones, Moon-like for moons, ice for comets,
    /// wood for world trees, and a main-sequence star's for stars.
    /// </summary>
    public static double TypicalDensity(BodyKind kind, double radiusKm) => kind switch
    {
        BodyKind.Star => StarDensity(radiusKm),
        BodyKind.Planet => PlanetDensity(radiusKm),
        BodyKind.Moon => MoonDensity,
        BodyKind.Comet => CometDensity,
        BodyKind.WorldTree => TreeDensity,
        _ => RockyDensity,
    };

    private static double PlanetDensity(double radiusKm)
    {
        double earthRadii = radiusKm / EarthRadiusKm;
        if (earthRadii <= LargestRockyRadii)
        {
            return RockyDensity;
        }

        if (earthRadii >= SmallestGiantRadii)
        {
            return GasGiantDensity;
        }

        // Even steps in log size between the two, so it slides smoothly.
        double along = Math.Log(earthRadii / LargestRockyRadii)
            / Math.Log(SmallestGiantRadii / LargestRockyRadii);
        return RockyDensity * Math.Pow(GasGiantDensity / RockyDensity, along);
    }

    private static double StarDensity(double radiusKm)
    {
        double suns = Math.Clamp(Math.Pow(radiusKm / SunRadiusKm, 1 / MainSequenceExponent),
            LightestStarSuns, HeaviestStarSuns);
        double density = suns * SunKg / GlobeVolumeM3(radiusKm) / 1000;
        return Math.Clamp(density, Body.MinDensityGramsPerCm3, Body.MaxDensityGramsPerCm3);
    }

    private static double GlobeVolumeM3(double radiusKm)
    {
        double radiusM = radiusKm * 1000;
        return 4.0 / 3 * Math.PI * radiusM * radiusM * radiusM;
    }
}
