using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public class LoreRulesTests
{
    [Fact]
    public void AWellLinkedWorld_HasNoProblem()
    {
        (World world, _, _, _) = LoreWorld();

        Assert.Null(LoreRules.Problem(world));
    }

    [Fact]
    public void EventsOnAMissingTimeline_AreAProblem()
    {
        (World world, _, _, _) = LoreWorld();
        world.Timelines.Clear();

        Assert.Contains("timeline", LoreRules.Problem(world));
    }

    [Fact]
    public void LinksToAMissingEntry_AreAProblem()
    {
        (World world, JournalEntry entry, _, _) = LoreWorld();
        world.Journal.Remove(entry);

        Assert.Contains("missing journal entry", LoreRules.Problem(world));
    }

    [Fact]
    public void PlacesOnAMissingBody_AreAProblem()
    {
        (World world, _, _, _) = LoreWorld();
        world.Journal.Add(new JournalEntry
        {
            Title = "Nowhere",
            Location = new LoreLocation(Guid.NewGuid()),
        });

        Assert.Contains("body that doesn't exist", LoreRules.Problem(world));
    }

    [Fact]
    public void SharedIds_AreAProblem()
    {
        (World world, JournalEntry entry, _, _) = LoreWorld();
        world.Journal.Add(entry with { Title = "Copy" });

        Assert.Contains("share an ID", LoreRules.Problem(world));
    }

    [Theory]
    [InlineData("", 0, null)]           // No title
    [InlineData("War", 10, 5.0)]        // Ends before it starts
    [InlineData("War", double.NaN, null)]
    public void BadEvents_AreAProblem(string title, double start, double? end)
    {
        var timelineEvent = new TimelineEvent
        {
            TimelineId = Guid.NewGuid(),
            Title = title,
            StartDays = start,
            EndDays = end,
        };

        Assert.NotNull(timelineEvent.Problem());
    }

    [Fact]
    public void AnEventLinkingAnEntryTwice_IsAProblem()
    {
        Guid entry = Guid.NewGuid();
        var timelineEvent = new TimelineEvent
        {
            TimelineId = Guid.NewGuid(),
            Title = "War",
            StartDays = 0,
            EntryIds = [entry, entry],
        };

        Assert.NotNull(timelineEvent.Problem());
    }

    [Fact]
    public void TooLongText_IsAProblem()
    {
        var entry = new JournalEntry
        {
            Title = "Epic",
            Text = new string('a', JournalEntry.MaxTextLength + 1),
        };

        Assert.NotNull(entry.Problem());
    }

    [Fact]
    public void EventsLinkedTo_FindsEveryEventLinkingAnEntry()
    {
        (World world, JournalEntry entry, TimelineEvent linked, _) = LoreWorld();
        world.Events.Add(new TimelineEvent
        {
            TimelineId = world.Timelines[0].Id,
            Title = "Unrelated",
            StartDays = 5,
        });

        Assert.Equal([linked], LoreRules.EventsLinkedTo(world, entry.Id));
    }

    [Fact]
    public void Events_AreEqualWhenEveryPartMatches_IncludingLinks()
    {
        (_, _, TimelineEvent linked, _) = LoreWorld();

        Assert.Equal(linked, linked with { EntryIds = [.. linked.EntryIds] });
        Assert.NotEqual(linked, linked with { EntryIds = [] });
        Assert.NotEqual(linked, linked with { EndDays = 99 });
    }

    [Fact]
    public void CloningAWorld_KeepsItsLore()
    {
        (World world, _, _, _) = LoreWorld();

        World copy = world.Clone();
        world.Journal.Clear();

        Assert.Single(copy.Journal);
        Assert.Equal(world.Timelines, copy.Timelines);
        Assert.Equal(world.Events, copy.Events);
    }

    [Fact]
    public void Regions_MustBeOnAPlanetOrMoon()
    {
        (World world, _, _, _) = LoreWorld();
        Guid sun = world.Bodies[1].Id;
        world.Regions.Add(Square(sun));

        Assert.Contains("star", LoreRules.Problem(world));
    }

    [Fact]
    public void APlaceInARegion_MustBeOnTheRegionsBody()
    {
        (World world, JournalEntry entry, _, _) = LoreWorld();
        Region region = Square(world.Bodies[0].Id);
        world.Regions.Add(region);
        world.Journal[0] = entry with
        {
            Location = new LoreLocation(world.Bodies[0].Id, RegionId: region.Id),
        };
        Assert.Null(LoreRules.Problem(world));

        world.Journal[0] = entry with
        {
            Location = new LoreLocation(world.Bodies[1].Id, RegionId: region.Id),
        };
        Assert.Contains("region that isn't on its body", LoreRules.Problem(world));
    }

    [Fact]
    public void RegionsAt_FindsTheRegionsContainingASpot()
    {
        (World world, _, _, _) = LoreWorld();
        Guid planet = world.Bodies[0].Id;
        Region region = Square(planet);
        world.Regions.Add(region);

        Assert.Equal([region], LoreRules.RegionsAt(world, planet, new GeoCoordinate(13, -30)));
        Assert.Empty(LoreRules.RegionsAt(world, planet, new GeoCoordinate(40, 40)));
    }

    [Fact]
    public void PlacedIn_ListsTheEntriesAndEventsInARegion()
    {
        (World world, JournalEntry entry, TimelineEvent linked, _) = LoreWorld();
        Guid planet = world.Bodies[0].Id;
        Region region = Square(planet);
        world.Regions.Add(region);
        world.Journal[0] = entry with { Location = new LoreLocation(planet, RegionId: region.Id) };

        (IEnumerable<JournalEntry> entries, IEnumerable<TimelineEvent> events) =
            LoreRules.PlacedIn(world, region.Id);

        Assert.Equal([world.Journal[0]], entries);
        Assert.Empty(events);
        Assert.DoesNotContain(linked, events);
    }

    [Theory]
    [InlineData(2)]     // Too few points
    [InlineData(1001)]  // Too many
    public void RegionsNeedAReasonableOutline(int corners)
    {
        var region = new Region
        {
            BodyId = Guid.NewGuid(),
            Name = "Somewhere",
            Corners = [.. Enumerable.Range(0, corners)
                .Select(i => new GeoCoordinate(10 * Math.Sin(i), 10 * Math.Cos(i)))],
        };

        Assert.NotNull(region.Problem());
    }

    private static Region Square(Guid bodyId) => new()
    {
        BodyId = bodyId,
        Name = "The Western Coast",
        Corners = [new(10, -35), new(10, -25), new(16, -25), new(16, -35)],
    };

    // A new world with one timeline, one entry pinned on the planet, and one event linked to it.
    private static (World World, JournalEntry Entry, TimelineEvent Event, Timeline Timeline)
        LoreWorld()
    {
        World world = World.CreateNew();
        var timeline = new Timeline { Name = "The Empire" };
        var entry = new JournalEntry
        {
            Title = "The Founding",
            Text = "Long ago.",
            Location = new LoreLocation(world.Bodies[0].Id, new GeoCoordinate(10, 20)),
        };
        var timelineEvent = new TimelineEvent
        {
            TimelineId = timeline.Id,
            Title = "Coronation",
            StartDays = 120,
            EndDays = 121,
            EntryIds = [entry.Id],
        };
        world.Timelines.Add(timeline);
        world.Journal.Add(entry);
        world.Events.Add(timelineEvent);
        return (world, entry, timelineEvent, timeline);
    }
}
