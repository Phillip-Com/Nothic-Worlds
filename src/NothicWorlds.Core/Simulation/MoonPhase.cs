using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// How one of a body's moons looks from the body at a moment (VISION.md CAL-05): how much of
/// its face is lit, and whether that's growing.
/// </summary>
/// <param name="MoonId">The moon.</param>
/// <param name="Name">The moon's name.</param>
/// <param name="Lit">How much of its face is lit, 0 (new) to 1 (full).</param>
/// <param name="Waxing">True while the lit part is growing (new toward full).</param>
public readonly record struct MoonPhase(Guid MoonId, string Name, double Lit, bool Waxing)
{
    // How far ahead to look to tell waxing from waning, in standard days.
    private const double LookAheadDays = 0.01;

    /// <summary>
    /// The phases of the moons circling <paramref name="body"/> at
    /// <paramref name="timeDays"/>, as seen from its middle, in the bodies' order. Empty if it
    /// has no moons, or no star to light them.
    /// </summary>
    public static IReadOnlyList<MoonPhase> Of(
        IReadOnlyList<Body> bodies, Body body, double timeDays)
    {
        List<Body> moons = bodies
            .Where(b => b.Kind == BodyKind.Moon && b.Orbit?.ParentId == body.Id)
            .ToList();
        if (moons.Count == 0 || Seasons.StarFor(bodies, body) is not Body star)
        {
            return [];
        }

        Dictionary<Guid, Vector3D> now = SystemPositions.At(bodies, timeDays);
        Dictionary<Guid, Vector3D> soon = SystemPositions.At(bodies, timeDays + LookAheadDays);
        return moons
            .Select(moon =>
            {
                double lit = LitFraction(now, star.Id, body.Id, moon.Id);
                bool waxing = LitFraction(soon, star.Id, body.Id, moon.Id) > lit;
                return new MoonPhase(moon.Id, moon.Name, lit, waxing);
            })
            .ToList();
    }

    /// <summary>
    /// The phase's everyday name: "New moon", "Waxing crescent", "First quarter", "Waxing
    /// gibbous", "Full moon", and the waning ones.
    /// </summary>
    public string PhaseName => Lit switch
    {
        < 0.03 => "New moon",
        > 0.97 => "Full moon",
        < 0.4 => Waxing ? "Waxing crescent" : "Waning crescent",
        <= 0.6 => Waxing ? "First quarter" : "Last quarter",
        _ => Waxing ? "Waxing gibbous" : "Waning gibbous",
    };

    // (1 + cos of the angle at the moon between its star and the body) / 2.
    private static double LitFraction(Dictionary<Guid, Vector3D> positions, Guid starId,
        Guid bodyId, Guid moonId)
    {
        Vector3D moon = positions[moonId];
        Vector3D toStar = positions[starId] - moon, toBody = positions[bodyId] - moon;
        double cos = toStar.Dot(toBody) / (toStar.Length * toBody.Length);
        return (1 + Math.Clamp(cos, -1, 1)) / 2;
    }
}
