using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public class SeasonTimelineTests
{
    [Fact]
    public void Answers_MatchWorkingThemOutEachTime()
    {
        (List<Body> bodies, Body planet) = EarthLike();
        SeasonTimeline timeline = SeasonTimeline.Around(bodies, planet, 1000);

        foreach (double time in new[] { 700.0, 830, 1000, 1111, 1300 })
        {
            Assert.Equal(Seasons.SeasonAt(bodies, planet, time), timeline.SeasonAt(time));
            Assert.Equal(Seasons.NextEvent(bodies, planet, time)!.Value.TimeDays,
                timeline.NextEvent(time)!.Value.TimeDays, 1e-6);
        }
    }

    [Fact]
    public void Covers_AYearEitherWay_ThenNeedsRedoing()
    {
        (List<Body> bodies, Body planet) = EarthLike();
        SeasonTimeline timeline = SeasonTimeline.Around(bodies, planet, 0);

        Assert.True(timeline.Covers(0));
        Assert.True(timeline.Covers(360));
        Assert.True(timeline.Covers(-360));
        Assert.False(timeline.Covers(400));
        Assert.False(timeline.Covers(-400));
    }

    [Fact]
    public void YearAround_StartsWithTheCurrentSeason()
    {
        (List<Body> bodies, Body planet) = EarthLike();
        SeasonTimeline timeline = SeasonTimeline.Around(bodies, planet, 0);

        IReadOnlyList<SeasonEvent> year = timeline.YearAround(100);

        Assert.Equal(
            [SeasonEventKind.NorthernSpringEquinox, SeasonEventKind.NorthernSummerSolstice,
                SeasonEventKind.NorthernAutumnEquinox, SeasonEventKind.NorthernWinterSolstice],
            year.Select(e => e.Kind));
        Assert.Equal(365.25 / 4, year[0].TimeDays, 0.05);
        Assert.Equal(365.25, year[^1].TimeDays, 0.05);
    }

    [Fact]
    public void YearAround_KeepsAnEventAfterGoingToIt()
    {
        (List<Body> bodies, Body planet) = EarthLike();
        SeasonTimeline timeline = SeasonTimeline.Around(bodies, planet, 0);
        SeasonEvent summer = timeline.YearAround(100)[1];

        Assert.Equal(summer, timeline.YearAround(summer.TimeDays)[0]);
    }

    [Fact]
    public void LatestEvent_BeganTheCurrentSeason()
    {
        (List<Body> bodies, Body planet) = EarthLike();
        SeasonTimeline timeline = SeasonTimeline.Around(bodies, planet, 0);

        SeasonEvent latest = timeline.LatestEvent(200)!.Value;

        Assert.Equal(SeasonEventKind.NorthernSummerSolstice, latest.Kind);
        Assert.Equal(timeline.YearAround(200)[0], latest);
    }

    [Fact]
    public void NoSeasons_GivesNoAnswers()
    {
        (List<Body> bodies, Body planet) = EarthLike();
        planet.AxialTiltDegrees = 0;
        SeasonTimeline timeline = SeasonTimeline.Around(bodies, planet, 0);

        Assert.Empty(timeline.Events);
        Assert.Null(timeline.SeasonAt(100));
        Assert.Null(timeline.NextEvent(100));
        Assert.Null(timeline.LatestEvent(100));
    }

    // The same sun and planet as SeasonsTests: northern winter solstice at time 0.
    private static (List<Body> Bodies, Body Planet) EarthLike()
    {
        var sun = new Body { Name = "Sun", Kind = BodyKind.Star, RadiusKm = 696_000 };
        var planet = new Body
        {
            Name = "Planet",
            AxialTiltDegrees = 23.4,
            Orbit = new Orbit { ParentId = sun.Id, DistanceKm = 149_600_000, PeriodDays = 365.25 },
        };
        return ([sun, planet], planet);
    }
}
