using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

// The checks use the real Sun, Earth, and Moon, whose eclipses are well known.
public class EclipsesTests
{
    private const double SynodicMonth = 29.530589;

    [Fact]
    public void FlatMoonOrbit_EclipsesEveryMonth()
    {
        // With the Moon's orbit in Earth's orbital plane, every new moon and full moon lines
        // up exactly: a solar and a lunar eclipse every month.
        (List<Body> bodies, Body earth, _) = EarthAndMoon(moonTiltDegrees: 0);

        List<Eclipse> eclipses = Eclipses.Between(bodies, earth, 1, 1 + 4 * SynodicMonth);
        List<Eclipse> solar = [.. eclipses.Where(e => e.Kind == EclipseKind.Solar)];
        List<Eclipse> lunar = [.. eclipses.Where(e => e.Kind == EclipseKind.Lunar)];

        Assert.Equal(4, solar.Count);
        Assert.Equal(4, lunar.Count);
        for (int i = 1; i < 4; i++)
        {
            Assert.Equal(SynodicMonth, solar[i].PeakDays - solar[i - 1].PeakDays, 0.01);
        }

        // Half a month between a solar eclipse and the next lunar one.
        Eclipse nextLunar = lunar.First(e => e.PeakDays > solar[0].PeakDays);
        Assert.Equal(SynodicMonth / 2, nextLunar.PeakDays - solar[0].PeakDays, 0.05);
    }

    [Fact]
    public void MoonAtItsAverageDistance_GivesAnnularSolarEclipses()
    {
        // At 384,400 km the Moon looks slightly smaller than the Sun: a ring of sunlight
        // stays, and about 94% of the Sun's disk is hidden.
        (List<Body> bodies, Body earth, _) = EarthAndMoon(moonTiltDegrees: 0);

        Eclipse solar = Eclipses.Between(bodies, earth, 1, 1 + SynodicMonth)
            .Single(e => e.Kind == EclipseKind.Solar);

        Assert.Equal(EclipseType.Annular, solar.Type);
        Assert.Equal(Math.Pow(1737.4 / 384_400 / (696_000 / 149_600_000.0), 2),
            solar.Coverage, 0.002);
    }

    [Fact]
    public void CloserMoon_GivesTotalSolarEclipses()
    {
        (List<Body> bodies, Body earth, Body moon) = EarthAndMoon(moonTiltDegrees: 0);
        moon.Orbit = moon.Orbit! with { DistanceKm = 360_000 };

        Eclipse solar = Eclipses.Between(bodies, earth, 1, 1 + SynodicMonth)
            .Single(e => e.Kind == EclipseKind.Solar);

        Assert.Equal(EclipseType.Total, solar.Type);
        Assert.Equal(1, solar.Coverage);
    }

    [Fact]
    public void CentralLunarEclipse_IsTotal_AndLastsAboutSixHours()
    {
        (List<Body> bodies, Body earth, _) = EarthAndMoon(moonTiltDegrees: 0);

        Eclipse lunar = Eclipses.Between(bodies, earth, 1, 1 + SynodicMonth)
            .Single(e => e.Kind == EclipseKind.Lunar);

        Assert.Equal(EclipseType.Total, lunar.Type);
        Assert.Equal(1, lunar.Coverage);
        double hours = (lunar.EndDays - lunar.StartDays) * 24;
        Assert.InRange(hours, 5.0, 6.5);
        Assert.True(lunar.StartDays < lunar.PeakDays && lunar.PeakDays < lunar.EndDays);
        // On circular orbits the eclipse is symmetric about its peak (within a minute).
        Assert.Equal(lunar.PeakDays - lunar.StartDays, lunar.EndDays - lunar.PeakDays,
            1.0 / 1440);
    }

    [Fact]
    public void TiltedMoonOrbit_GivesAFewEclipsesAYear()
    {
        // Earth's real tilt: eclipses happen only when the new or full moon falls near where
        // the orbits cross, so there are 2 to 5 solar eclipses a year, not 12.
        (List<Body> bodies, Body earth, _) = EarthAndMoon(moonTiltDegrees: 5.145);

        List<Eclipse> eclipses = Eclipses.Between(bodies, earth, 0, 365.25);

        Assert.InRange(eclipses.Count(e => e.Kind == EclipseKind.Solar), 2, 5);
        Assert.InRange(eclipses.Count(e => e.Kind == EclipseKind.Lunar), 2, 5);
    }

    [Fact]
    public void ShallowPass_IsPartialOrPenumbral()
    {
        // The Moon passes just below the shadows' centers: only part of it enters Earth's
        // full shadow, and it covers only part of the Sun.
        (List<Body> bodies, Body earth, Body moon) = EarthAndMoon(moonTiltDegrees: 1.3);
        moon.Orbit = moon.Orbit! with { TiltDirectionDegrees = 90 };

        List<Eclipse> eclipses = Eclipses.Between(bodies, earth, 1, 1 + SynodicMonth);

        Eclipse solar = eclipses.Single(e => e.Kind == EclipseKind.Solar);
        Eclipse lunar = eclipses.Single(e => e.Kind == EclipseKind.Lunar);
        Assert.Equal(EclipseType.Partial, solar.Type);
        Assert.InRange(solar.Coverage, 0.01, 0.99);
        Assert.Contains(lunar.Type, new[] { EclipseType.Partial, EclipseType.Penumbral });
    }

    [Theory]
    [InlineData(1.43, true)]   // Passes ~9,600 km from the shadow's center: just touches Earth
    [InlineData(1.52, false)]  // ~10,200 km: just misses (the shadow reaches ~9,900 km)
    public void GrazingPasses_AreFoundOrMissed(double moonTiltDegrees, bool touches)
    {
        // The search skips passes it can tell won't reach the shadow; it mustn't skip one that
        // barely does. The first new moon comes at ~14.8 days, with Earth ~14.6° along its
        // orbit; the Moon's orbit is turned so it's at its farthest above Earth's orbit then.
        (List<Body> bodies, Body earth, Body moon) = EarthAndMoon(moonTiltDegrees);
        moon.Orbit = moon.Orbit! with { TiltDirectionDegrees = 90 + 14.56 };

        List<Eclipse> eclipses = Eclipses.Between(bodies, earth, 5, 25);

        Assert.Equal(touches ? 1 : 0, eclipses.Count);
        if (touches)
        {
            Eclipse solar = eclipses[0];
            Assert.Equal(EclipseKind.Solar, solar.Kind);
            Assert.Equal(EclipseType.Partial, solar.Type);
            Assert.InRange(solar.Coverage, 0.0, 0.1);
            Assert.InRange((solar.EndDays - solar.StartDays) * 24, 0.5, 2.5);
        }
    }

    [Fact]
    public void Moon_HasTheSameEclipsesAsItsPlanet()
    {
        // Lunar: the Moon in Earth's shadow. Solar: the Moon in front of the Sun, seen from
        // Earth (owner's request: the selected moon shows both).
        (List<Body> bodies, Body earth, Body moon) = EarthAndMoon(moonTiltDegrees: 5.145);

        List<Eclipse> fromEarth = Eclipses.Between(bodies, earth, 0, 365.25);
        List<Eclipse> fromMoon = Eclipses.Between(bodies, moon, 0, 365.25);

        Assert.Equal(fromEarth, fromMoon);
        Assert.Contains(fromMoon, e => e.Kind == EclipseKind.Solar);
        Assert.Contains(fromMoon, e => e.Kind == EclipseKind.Lunar);
    }

    [Fact]
    public void PlanetCenteredSystem_HasTheSameEclipses()
    {
        (List<Body> bodies, Body earth, _) = EarthAndMoon(moonTiltDegrees: 5.145);
        List<Eclipse> before = Eclipses.Between(bodies, earth, 0, 365.25);

        foreach ((Guid id, Orbit? orbit) in SystemHierarchy.MakeCenter(bodies, earth.Id))
        {
            bodies.First(b => b.Id == id).Orbit = orbit;
        }

        List<Eclipse> after = Eclipses.Between(bodies, earth, 0, 365.25);
        Assert.Equal(before.Count, after.Count);
        for (int i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].Type, after[i].Type);
            Assert.Equal(before[i].PeakDays, after[i].PeakDays, 1e-6);
        }
    }

    [Fact]
    public void StarsAndMoonlessPlanets_HaveNoEclipses()
    {
        (List<Body> bodies, Body earth, Body moon) = EarthAndMoon(moonTiltDegrees: 0);

        Assert.Empty(Eclipses.Between(bodies, bodies[0], 0, 100));
        bodies.Remove(moon);
        Assert.Empty(Eclipses.Between(bodies, earth, 0, 100));
    }

    [Fact]
    public void SameWorldAndTime_AlwaysGivesTheSameEclipses()
    {
        (List<Body> bodies, Body earth, _) = EarthAndMoon(moonTiltDegrees: 5.145);

        Assert.Equal(Eclipses.Between(bodies, earth, 0, 400),
            Eclipses.Between(bodies, earth, 0, 400));
    }

    [Fact]
    public void Timeline_MatchesTheSearch_AndListsWhatsComing()
    {
        (List<Body> bodies, Body earth, _) = EarthAndMoon(moonTiltDegrees: 0);
        EclipseTimeline timeline = EclipseTimeline.Around(bodies, earth, 100);

        Assert.True(timeline.Covers(100));
        Assert.False(timeline.Covers(500));
        // It looks back a quarter year, for each moon's previous eclipse.
        Assert.True(timeline.Covers(100 - 365.25 * 0.24));
        Assert.False(timeline.Covers(100 - 365.25 * 0.26));
        Assert.Equal(Eclipses.Between(bodies, earth, timeline.FromDays, timeline.ToDays),
            timeline.Eclipses);

        IReadOnlyList<Eclipse> coming = timeline.Upcoming(100);
        Assert.All(coming, e => Assert.InRange(e.PeakDays, 100 - 1, 100 + 365.25));
        // About 12 of each in a year of flat orbits.
        Assert.InRange(coming.Count, 24, 26);

        // An eclipse under way still counts as coming (Go to lands on its peak).
        Eclipse first = coming[0];
        Assert.Equal(first, timeline.Upcoming(first.PeakDays)[0]);
    }

    // The Sun, Earth (starting at +X from the Sun), and the Moon (starting at +X from Earth,
    // so a full moon at time 0).
    private static (List<Body> Bodies, Body Earth, Body Moon) EarthAndMoon(double moonTiltDegrees)
    {
        var sun = new Body { Name = "Sun", Kind = BodyKind.Star, RadiusKm = 696_000 };
        var earth = new Body
        {
            Name = "Earth",
            RadiusKm = 6371,
            Orbit = new Orbit { ParentId = sun.Id, DistanceKm = 149_600_000, PeriodDays = 365.25 },
        };
        var moon = new Body
        {
            Name = "Moon",
            Kind = BodyKind.Moon,
            RadiusKm = 1737.4,
            Orbit = new Orbit
            {
                ParentId = earth.Id,
                DistanceKm = 384_400,
                PeriodDays = 27.321661,
                TiltDegrees = moonTiltDegrees,
            },
        };
        return ([sun, earth, moon], earth, moon);
    }
}
