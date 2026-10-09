using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// The fine relief of a rough terrain type up close (VISION.md BOD-12; owner's choice: a share
/// of the type's own variation): its features (<see cref="TerrainNoise"/>) carried on below
/// the smallest the height grid holds, down to a few meters, so mountains are craggy and plains
/// a little uneven underfoot. Made when the ground is drawn, never saved. The same seed and
/// place always give the same value.
/// </summary>
/// <remarks>
/// <b>Never change how the relief is made</b> without a format change: worlds are saved with
/// only their settings, so the ground they show depends on it (docs/world-format.md).
/// </remarks>
public static class TerrainRoughness
{
    /// <summary>
    /// How far from a river its features can be faded (see <see cref="Offset"/>), in meters,
    /// on a body <paramref name="radiusKm"/> in radius: the biggest are a little under three
    /// of the height grid's cells across.
    /// </summary>
    public static double RiverReachMeters(double radiusKm) =>
        3 * radiusKm * 1000 * Math.PI / 2 / TerrainGrid.FaceSize;

    /// <summary>
    /// The finest features made, in meters: finer bumps are the ground's shading
    /// (planet_surface.gdshaderinc), which is cheaper and never shimmers.
    /// </summary>
    public const double FinestMeters = 20;

    // More layers than any size could need (from 5,000 km down to FinestMeters is 18), so a
    // bad setting can't loop for long.
    private const int MaxLayers = 40;

    /// <summary>
    /// How far the fine relief lifts (positive) or sinks the ground at a place, in meters.
    /// </summary>
    /// <param name="direction">The place: a unit direction from the body's center.</param>
    /// <param name="radiusKm">The body's radius.</param>
    /// <param name="variationMeters">The type's variation (0 gives 0).</param>
    /// <param name="sizeKm">
    /// How far apart its biggest features are, as <see cref="TerrainNoise.Offset"/> was given.
    /// </param>
    /// <param name="belowKm">
    /// Where <see cref="TerrainNoise.Offset"/>'s features stopped (its smallest): the relief
    /// starts with the first layer finer than this.
    /// </param>
    /// <param name="roughness">The type's roughness, 0 (none) to 1 (craggy).</param>
    /// <param name="smallestMeters">
    /// The smallest features wanted (at least <see cref="FinestMeters"/>): the ground drawn
    /// coarsely leaves out what it couldn't show, so it doesn't shimmer.
    /// </param>
    /// <param name="seed">The type's seed, as given to <see cref="TerrainNoise.Offset"/>.</param>
    /// <param name="riverMeters">
    /// How far the place is past the nearest river's banks (infinite with none near): each
    /// feature fades out within its own size of a river, so rivers run in smooth valleys as
    /// wide as the features around them, with fine crags right up to their banks.
    /// </param>
    public static double Offset(Vector3D direction, double radiusKm, double variationMeters,
        double sizeKm, double belowKm, double roughness, double smallestMeters, int seed,
        double riverMeters = double.PositiveInfinity)
    {
        if (variationMeters <= 0 || sizeKm <= 0 || roughness <= 0)
        {
            return 0;
        }

        double smallest = Math.Max(smallestMeters, FinestMeters);
        double sizeMeters = sizeKm * 1000, belowMeters = belowKm * 1000;

        // At half roughness the first layer is as big as the type's own layers would make it,
        // and finer layers shrink more slowly than they do (craggier as roughness rises): each
        // by half to the power of the falloff.
        double falloff = 1 - 0.25 * roughness;
        double shrink = Math.Pow(0.5, falloff);
        double ridged = TerrainNoise.SmoothStep((variationMeters - TerrainNoise.RidgedFromMeters)
            / (TerrainNoise.FullyRidgedMeters - TerrainNoise.RidgedFromMeters));
        Vector3D p = direction * (radiusKm / sizeKm);
        double wavelength = sizeMeters, amplitude = 0, total = 0;
        for (int layer = 0; layer < MaxLayers && wavelength >= smallest; layer++)
        {
            if (wavelength < belowMeters)
            {
                amplitude = amplitude == 0
                    ? roughness * variationMeters * (wavelength / sizeMeters)
                    : amplitude * shrink;

                // The finest layer fades in, so the ground drawn more coarsely farther off
                // meets the finer ground nearer without a step.
                double fade = Math.Min(wavelength / smallest - 1, 1)
                    * TerrainNoise.SmoothStep(riverMeters / wavelength);
                double n = TerrainNoise.Value(p, seed + layer * 101);  // 0 to 1
                double smooth = 2 * n - 1;
                double ridge = 1 - Math.Abs(smooth);
                double sharp = 2 * ridge * ridge - 1;
                total += amplitude * fade * (smooth + (sharp - smooth) * ridged);
            }

            wavelength *= 0.5;
            p = p * 2.03 + new Vector3D(17.1, 3.7, 9.4);
        }

        return total;
    }
}
