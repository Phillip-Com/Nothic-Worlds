using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public class TimeRulerTests
{
    [Fact]
    public void ADay_IsMarkedInHours()
    {
        var body = new Body();  // 24-hour days, no calendar

        List<RulerTick> ticks = TimeRuler.Ticks(body, 0, 1, maxTicks: 10);

        Assert.Equal(["0:00", "3:00", "6:00", "9:00", "12:00", "15:00", "18:00", "21:00", "0:00"],
            ticks.Select(t => t.Label));
        Assert.Equal(0.125, ticks[1].TimeDays, 1e-12);
    }

    [Fact]
    public void WithoutACalendar_DaysAreNumbered()
    {
        var body = new Body();

        List<RulerTick> ticks = TimeRuler.Ticks(body, 0.5, 100, maxTicks: 10);

        // Every 10th day, counted from day 1 at time 0.
        Assert.Equal("Day 11", ticks[0].Label);
        Assert.Equal(10, ticks[0].TimeDays, 1e-12);
        Assert.Equal(10, ticks.Count);
    }

    [Fact]
    public void LongSpans_UseRoundSteps()
    {
        var body = new Body();

        List<RulerTick> ticks = TimeRuler.Ticks(body, 0, 1_000_000, maxTicks: 8);

        Assert.InRange(ticks.Count, 1, 8);
        Assert.Equal(200_000, ticks[1].TimeDays - ticks[0].TimeDays, 1e-6);
    }

    [Fact]
    public void ACalendarsMonths_AreLabelledByName()
    {
        Body body = WithCalendar();

        List<RulerTick> ticks = TimeRuler.Ticks(body, 0, 400, maxTicks: 10);

        Assert.Equal(["Frost 1203", "Thaw 1203", "Sun 1203", "Fall 1203", "Frost 1204"],
            ticks.Select(t => t.Label));
        Assert.Equal(91, ticks[1].TimeDays, 1e-9);
    }

    [Fact]
    public void ACalendarsYears_AreRoundNumbers()
    {
        Body body = WithCalendar();

        List<RulerTick> ticks = TimeRuler.Ticks(body, 0, 364 * 40, maxTicks: 10);

        // A 40-year span in steps of 5: the first round year after 1203 is 1205.
        Assert.Equal("1205", ticks[0].Label);
        Assert.Equal(364 * 2, ticks[0].TimeDays, 1e-9);
        Assert.All(ticks, t => Assert.Equal(0, long.Parse(t.Label) % 5));
    }

    [Fact]
    public void ACalendarsDays_ShowTheDate()
    {
        Body body = WithCalendar();

        List<RulerTick> ticks = TimeRuler.Ticks(body, 0, 20, maxTicks: 10);

        Assert.Equal("1 Frost", ticks[0].Label);
        Assert.Equal("3 Frost", ticks[1].Label);
    }

    [Fact]
    public void NoSpan_OrNoRoom_GivesNoTicks()
    {
        Assert.Empty(TimeRuler.Ticks(new Body(), 5, 5, maxTicks: 10));
        Assert.Empty(TimeRuler.Ticks(new Body(), 0, 5, maxTicks: 0));
    }

    // 24-hour days; four 91-day months starting on 1 Frost 1203 at time 0.
    private static Body WithCalendar()
    {
        return new Body
        {
            Calendar = new Calendar
            {
                Months = [new("Frost", 91), new("Thaw", 91), new("Sun", 91), new("Fall", 91)],
                FirstYear = 1203,
            },
        };
    }
}
