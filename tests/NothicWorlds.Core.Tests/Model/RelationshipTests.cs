using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public class RelationshipTests
{
    [Fact]
    public void AWorldWithRelationshipsAndDiagrams_HasNoProblem()
    {
        (World world, _, _, _) = HouseWorld();

        Assert.Null(LoreRules.Problem(world));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void RelationshipsToAMissingEntry_AreAProblem(bool fromMissing, bool toMissing)
    {
        (World world, JournalEntry king, JournalEntry prince, _) = HouseWorld();
        world.Relationships.Add(new Relationship
        {
            FromEntryId = fromMissing ? Guid.NewGuid() : king.Id,
            ToEntryId = toMissing ? Guid.NewGuid() : prince.Id,
            Kind = RelationshipKind.Serves,
        });

        Assert.Contains("missing journal entry", LoreRules.Problem(world));
    }

    [Fact]
    public void DiagramsShowingAMissingEntry_AreAProblem()
    {
        (World world, _, _, _) = HouseWorld();
        world.Diagrams.Add(new LoreDiagram
        {
            Name = "Strangers",
            Placements = [new DiagramPlacement(Guid.NewGuid(), 0, 0)],
        });

        Assert.Contains("missing journal entry", LoreRules.Problem(world));
    }

    [Fact]
    public void BadRelationships_AreAProblem()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        var good = new Relationship
        {
            FromEntryId = a,
            ToEntryId = b,
            Kind = RelationshipKind.AllyOf,
        };

        Assert.Null(good.Problem());
        Assert.NotNull((good with { ToEntryId = a }).Problem());  // With itself
        Assert.NotNull((good with { Kind = RelationshipKind.Other }).Problem());  // No words
        Assert.Null((good with { Kind = RelationshipKind.Other, Label = "owes" }).Problem());
        Assert.NotNull((good with { Label = new string('x', 101) }).Problem());
        Assert.NotNull((good with { Kind = (RelationshipKind)99 }).Problem());
        Assert.NotNull((good with { StartDays = 10, EndDays = 5 }).Problem());
        Assert.NotNull((good with { StartDays = double.NaN }).Problem());
        Assert.NotNull((good with { EndDays = double.PositiveInfinity }).Problem());
        Assert.Null((good with { StartDays = 5, EndDays = 5 }).Problem());
    }

    [Fact]
    public void BadDiagrams_AreAProblem()
    {
        Guid a = Guid.NewGuid();
        var good = new LoreDiagram { Name = "Court", Placements = [new(a, 10, -20)] };

        Assert.Null(good.Problem());
        Assert.NotNull((good with { Name = " " }).Problem());
        Assert.NotNull((good with { Name = new string('x', 101) }).Problem());
        Assert.NotNull((good with { Placements = [new(a, 0, 0), new(a, 5, 5)] }).Problem());
        Assert.NotNull((good with { Placements = [new(a, double.NaN, 0)] }).Problem());
        Assert.NotNull((good with { Placements = [new(a, 0, 2e6)] }).Problem());
    }

    [Fact]
    public void AnEntryOfAnUnknownKind_IsAProblem()
    {
        var entry = new JournalEntry { Title = "Mystery", Kind = (LoreKind)42 };

        Assert.NotNull(entry.Problem());
        Assert.Null((entry with { Kind = LoreKind.Faction }).Problem());
        Assert.Null((entry with { Kind = null }).Problem());
    }

    [Fact]
    public void SharedRelationshipOrDiagramIds_AreAProblem()
    {
        (World world, _, _, Relationship parent) = HouseWorld();
        world.Relationships.Add(parent with { Label = "a copy" });

        Assert.Contains("share an ID", LoreRules.Problem(world));

        world.Relationships.RemoveAt(world.Relationships.Count - 1);
        world.Diagrams.Add(world.Diagrams[0] with { Name = "Copy" });

        Assert.Contains("share an ID", LoreRules.Problem(world));
    }

    [Theory]
    [InlineData(null, null, 50, true)]
    [InlineData(10.0, null, 9.9, false)]
    [InlineData(10.0, null, 10, true)]
    [InlineData(null, 20.0, 20, true)]
    [InlineData(null, 20.0, 20.1, false)]
    [InlineData(10.0, 20.0, 15, true)]
    public void ARelationship_StandsBetweenItsStartAndEnd(double? start, double? end,
        double time, bool stands)
    {
        var tie = new Relationship
        {
            FromEntryId = Guid.NewGuid(),
            ToEntryId = Guid.NewGuid(),
            Kind = RelationshipKind.AtWarWith,
            StartDays = start,
            EndDays = end,
        };

        Assert.Equal(stands, tie.StandsAt(time));
    }

    [Theory]
    [InlineData(RelationshipKind.MarriedTo, true)]
    [InlineData(RelationshipKind.SiblingOf, true)]
    [InlineData(RelationshipKind.AllyOf, true)]
    [InlineData(RelationshipKind.RivalOf, true)]
    [InlineData(RelationshipKind.AtWarWith, true)]
    [InlineData(RelationshipKind.ParentOf, false)]
    [InlineData(RelationshipKind.MemberOf, false)]
    [InlineData(RelationshipKind.Rules, false)]
    [InlineData(RelationshipKind.Serves, false)]
    [InlineData(RelationshipKind.Other, false)]
    public void SomeKinds_AreMutual(RelationshipKind kind, bool mutual)
    {
        var tie = new Relationship
        {
            FromEntryId = Guid.NewGuid(),
            ToEntryId = Guid.NewGuid(),
            Kind = kind,
        };

        Assert.Equal(mutual, tie.IsMutual);
    }

    [Fact]
    public void RelationshipsOf_FindsAnEntrysTiesEitherWay()
    {
        (World world, JournalEntry king, JournalEntry prince, Relationship parent) = HouseWorld();
        JournalEntry rival = AddEntry(world, "House Venn", LoreKind.Faction);
        var feud = new Relationship
        {
            FromEntryId = rival.Id,
            ToEntryId = prince.Id,
            Kind = RelationshipKind.RivalOf,
        };
        world.Relationships.Add(feud);

        Assert.Equal([parent, feud], LoreRules.RelationshipsOf(world, prince.Id));
        Assert.Equal([parent], LoreRules.RelationshipsOf(world, king.Id));
    }

    [Fact]
    public void ForgettingAnEntry_RemovesEverythingThatPointsAtIt()
    {
        (World world, JournalEntry king, JournalEntry prince, _) = HouseWorld();
        world.Timelines.Add(new Timeline { Name = "Reigns", Color = new RgbColor(1, 2, 3) });
        world.Events.Add(new TimelineEvent
        {
            TimelineId = world.Timelines[0].Id,
            Title = "Crowning",
            StartDays = 3,
            EntryIds = [king.Id, prince.Id],
        });

        world.Journal.Remove(king);
        LoreRules.ForgetEntry(world, king.Id);

        Assert.Null(LoreRules.Problem(world));
        Assert.Empty(world.Relationships);
        Assert.Equal([prince.Id], world.Events[0].EntryIds);
        Assert.Equal([prince.Id], world.Diagrams[0].Placements.Select(p => p.EntryId));
    }

    [Fact]
    public void Diagrams_AreEqualWhenEveryPartMatches_IncludingPlacements()
    {
        (World world, _, _, _) = HouseWorld();
        LoreDiagram diagram = world.Diagrams[0];

        Assert.Equal(diagram, diagram with { Placements = [.. diagram.Placements] });
        Assert.NotEqual(diagram, diagram with { Placements = [diagram.Placements[0]] });
        Assert.NotEqual(diagram, diagram with
        {
            Placements = [diagram.Placements[0] with { X = 1 }, diagram.Placements[1]],
        });
    }

    [Fact]
    public void CloningAWorld_KeepsItsRelationshipsAndDiagrams()
    {
        (World world, _, _, _) = HouseWorld();

        World copy = world.Clone();
        world.Relationships.Clear();
        world.Diagrams.Clear();

        Assert.Single(copy.Relationships);
        Assert.Single(copy.Diagrams);
    }

    // A king and his son, the tie between them, and a diagram of the two.
    private static (World World, JournalEntry King, JournalEntry Prince, Relationship Parent)
        HouseWorld()
    {
        World world = World.CreateNew();
        JournalEntry king = AddEntry(world, "King Aldric", LoreKind.Character);
        JournalEntry prince = AddEntry(world, "Prince Edran", LoreKind.Character);
        var parent = new Relationship
        {
            FromEntryId = king.Id,
            ToEntryId = prince.Id,
            Kind = RelationshipKind.ParentOf,
            StartDays = 100,
        };
        world.Relationships.Add(parent);
        world.Diagrams.Add(new LoreDiagram
        {
            Name = "House Arren",
            Placements = [new(king.Id, 0, 0), new(prince.Id, 0, 120)],
        });
        return (world, king, prince, parent);
    }

    private static JournalEntry AddEntry(World world, string title, LoreKind kind)
    {
        var entry = new JournalEntry { Title = title, Kind = kind };
        world.Journal.Add(entry);
        return entry;
    }
}
