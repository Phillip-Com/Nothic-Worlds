using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public class DiagramLayoutTests
{
    private static readonly Guid _king = Guid.NewGuid(), _queen = Guid.NewGuid(),
        _prince = Guid.NewGuid(), _princess = Guid.NewGuid(), _bride = Guid.NewGuid(),
        _heir = Guid.NewGuid(), _order = Guid.NewGuid(), _knight = Guid.NewGuid(),
        _stranger = Guid.NewGuid();

    // Three generations: the king and queen, their son and daughter, the son's bride, their
    // heir; and outside the family, an order, a knight in it, and a stranger.
    private static readonly Relationship[] _ties =
    [
        Tie(_king, _queen, RelationshipKind.MarriedTo),
        Tie(_king, _prince, RelationshipKind.ParentOf),
        Tie(_queen, _prince, RelationshipKind.ParentOf),
        Tie(_king, _princess, RelationshipKind.ParentOf),
        Tie(_queen, _princess, RelationshipKind.ParentOf),
        Tie(_prince, _princess, RelationshipKind.SiblingOf),
        Tie(_bride, _prince, RelationshipKind.MarriedTo),
        Tie(_prince, _heir, RelationshipKind.ParentOf),
        Tie(_bride, _heir, RelationshipKind.ParentOf),
        Tie(_knight, _order, RelationshipKind.MemberOf),
    ];

    private static readonly Guid[] _entries =
        [_heir, _stranger, _knight, _bride, _prince, _order, _king, _princess, _queen];

    [Fact]
    public void Parents_StandARowAboveTheirChildren()
    {
        var at = Arrange();

        Assert.Equal(at[_king].Y, at[_queen].Y);
        Assert.Equal(at[_king].Y + DiagramLayout.RowSpacing, at[_prince].Y);
        Assert.Equal(at[_prince].Y, at[_princess].Y);
        Assert.Equal(at[_prince].Y + DiagramLayout.RowSpacing, at[_heir].Y);
    }

    [Fact]
    public void Spouses_StandSideBySide()
    {
        var at = Arrange();

        Assert.Equal(DiagramLayout.ColumnSpacing, Math.Abs(at[_king].X - at[_queen].X));
        Assert.Equal(at[_prince].Y, at[_bride].Y);  // Married in: in her husband's row
        Assert.Equal(DiagramLayout.ColumnSpacing, Math.Abs(at[_prince].X - at[_bride].X));
    }

    [Fact]
    public void AnOnlyChild_StandsUnderItsParents()
    {
        var at = Arrange();

        Assert.InRange(at[_heir].X, Math.Min(at[_prince].X, at[_bride].X),
            Math.Max(at[_prince].X, at[_bride].X));
    }

    [Fact]
    public void TheRest_GoBelowTheTree_WithTiedEntriesTogether()
    {
        var at = Arrange();
        double treeBottom = new[] { _king, _queen, _prince, _princess, _bride, _heir }
            .Max(id => at[id].Y);

        Assert.All([_order, _knight, _stranger], id => Assert.True(at[id].Y > treeBottom));
        // In neighboring cells of the grid (beside, below, or one across and one down).
        Assert.True(Distance(at[_order], at[_knight]) <= Math.Sqrt(
            DiagramLayout.ColumnSpacing * DiagramLayout.ColumnSpacing
            + DiagramLayout.RowSpacing * DiagramLayout.RowSpacing) + 1e-9);
    }

    [Fact]
    public void NoTwoBoxes_Overlap()
    {
        var at = Arrange();

        foreach (Guid a in _entries)
        {
            foreach (Guid b in _entries.Where(b => b != a))
            {
                Assert.True(at[a].Y != at[b].Y
                    || Math.Abs(at[a].X - at[b].X) >= DiagramLayout.ColumnSpacing - 1e-9,
                    "two boxes in one row are too close");
            }
        }
    }

    [Fact]
    public void EveryEntryIsPlaced_InTheDiagramsOrder_TheSameEveryTime()
    {
        IReadOnlyList<DiagramPlacement> first = DiagramLayout.Arrange(_entries, _ties);
        IReadOnlyList<DiagramPlacement> second = DiagramLayout.Arrange(_entries, _ties);

        Assert.Equal(_entries, first.Select(p => p.EntryId));
        Assert.Equal(first, second);
    }

    [Fact]
    public void TiesToEntriesNotOnTheDiagram_AreIgnored()
    {
        IReadOnlyList<DiagramPlacement> placements =
            DiagramLayout.Arrange([_prince, _princess], _ties);

        Assert.Equal(placements[0].Y, placements[1].Y);  // Siblings, their parents not shown
    }

    [Fact]
    public void ALoopOfParents_StillGetsALayout()
    {
        Relationship[] loop =
        [
            Tie(_king, _queen, RelationshipKind.ParentOf),
            Tie(_queen, _prince, RelationshipKind.ParentOf),
            Tie(_prince, _king, RelationshipKind.ParentOf),
        ];

        IReadOnlyList<DiagramPlacement> placements =
            DiagramLayout.Arrange([_king, _queen, _prince], loop);

        Assert.Equal(3, placements.Count);
        Assert.All(placements, p => Assert.True(double.IsFinite(p.X) && double.IsFinite(p.Y)));
    }

    [Fact]
    public void WithoutFamily_EverythingIsInAGridAroundTheMiddle()
    {
        IReadOnlyList<DiagramPlacement> placements =
            DiagramLayout.Arrange([_order, _knight, _stranger, _heir], [_ties[^1]]);

        Assert.Equal(0, placements.Min(p => p.Y));
        Assert.Equal(2, placements.Select(p => p.Y).Distinct().Count());  // 2 × 2
        Assert.Equal(0, placements.Where(p => p.Y == 0).Sum(p => p.X), 6);  // Centered
    }

    private static Dictionary<Guid, (double X, double Y)> Arrange() =>
        DiagramLayout.Arrange(_entries, _ties).ToDictionary(p => p.EntryId, p => (p.X, p.Y));

    private static double Distance((double X, double Y) a, (double X, double Y) b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static Relationship Tie(Guid from, Guid to, RelationshipKind kind) =>
        new() { FromEntryId = from, ToEntryId = to, Kind = kind };
}
