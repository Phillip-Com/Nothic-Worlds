namespace NothicWorlds.Core.Model;

/// <summary>
/// The Arrange button's tidy layout for a lore diagram (VISION.md LORE-04; owner's choice: a
/// family tree, then the rest). Entries tied by family (parent of, married to, sibling of) are
/// laid out in rows by generation, parents above their children, spouses side by side, each
/// row shifted to sit under the parents above it. Everything else goes in a grid below the
/// tree, in an order that keeps entries tied to each other near each other. Deterministic: the
/// same entries and ties always give the same layout.
/// </summary>
public static class DiagramLayout
{
    /// <summary>How far apart neighbors in a row are, middle to middle, in diagram units.</summary>
    public const double ColumnSpacing = 200;

    /// <summary>How far apart rows are, middle to middle, in diagram units.</summary>
    public const double RowSpacing = 140;

    /// <summary>
    /// Lays out <paramref name="entries"/> (a diagram's, in its order) by the
    /// <paramref name="relationships"/> among them (any others are ignored), dated or not: a
    /// parent stays a parent after death. Returns a placement for every entry, in the same
    /// order.
    /// </summary>
    public static IReadOnlyList<DiagramPlacement> Arrange(IReadOnlyList<Guid> entries,
        IEnumerable<Relationship> relationships)
    {
        var order = new Dictionary<Guid, int>();
        foreach (Guid entry in entries)
        {
            order.TryAdd(entry, order.Count);
        }

        List<Relationship> ties = [.. relationships.Where(r => order.ContainsKey(r.FromEntryId)
            && order.ContainsKey(r.ToEntryId) && r.FromEntryId != r.ToEntryId)];
        List<Relationship> family = [.. ties.Where(IsFamily)];
        var inFamily = family.SelectMany(r => new[] { r.FromEntryId, r.ToEntryId }).ToHashSet();

        var placed = new Dictionary<Guid, (double X, double Y)>();
        int rows = PlaceFamily(order, family, inFamily, placed);
        PlaceOthers(order, ties, inFamily, placed, rows);
        return [.. order.Keys.Select(id => new DiagramPlacement(id, placed[id].X, placed[id].Y))];
    }

    private static bool IsFamily(Relationship tie) => tie.Kind is RelationshipKind.ParentOf
        or RelationshipKind.MarriedTo or RelationshipKind.SiblingOf;

    // Lays out the family tree, top row at y = 0; returns how many rows it took.
    private static int PlaceFamily(Dictionary<Guid, int> order, List<Relationship> family,
        HashSet<Guid> inFamily, Dictionary<Guid, (double X, double Y)> placed)
    {
        if (inFamily.Count == 0)
        {
            return 0;
        }

        Dictionary<Guid, int> generation = Generations(family, inFamily);
        var parents = family.Where(r => r.Kind == RelationshipKind.ParentOf)
            .GroupBy(r => r.ToEntryId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.FromEntryId).ToList());
        var spouses = family.Where(r => r.Kind == RelationshipKind.MarriedTo)
            .SelectMany(r => new[] { (r.FromEntryId, r.ToEntryId), (r.ToEntryId, r.FromEntryId) })
            .Where(pair => generation[pair.Item1] == generation[pair.Item2])
            .GroupBy(pair => pair.Item1)
            .ToDictionary(g => g.Key, g => g.Select(pair => pair.Item2).ToList());

        int rows = generation.Values.Max() + 1;
        for (int row = 0; row < rows; row++)
        {
            List<Guid> members = [.. generation.Where(g => g.Value == row).Select(g => g.Key)];
            // Where each would like to be: under its parents placed above, else in its turn.
            var wanted = members.ToDictionary(id => id, id =>
                parents.TryGetValue(id, out List<Guid>? its)
                    && its.Where(placed.ContainsKey).ToList() is { Count: > 0 } above
                    ? above.Average(p => placed[p].X)
                    : double.NaN);
            List<Guid> sorted = KeepSpousesTogether(
                [.. members.OrderBy(id => SortKey(id, wanted, spouses, order))
                    .ThenBy(id => order[id])], spouses);
            PlaceRow(sorted, wanted, row * RowSpacing, placed);
        }

        return rows;
    }

    // Each family member's row: parents above children, spouses and siblings level, the top
    // row 0. A loop of parents (a mistake, or magic) is cut off after enough passes.
    private static Dictionary<Guid, int> Generations(List<Relationship> family,
        HashSet<Guid> members)
    {
        var generation = members.ToDictionary(id => id, _ => 0);
        for (int pass = 0; pass <= members.Count; pass++)
        {
            bool changed = false;
            foreach (Relationship tie in family)
            {
                int from = generation[tie.FromEntryId], to = generation[tie.ToEntryId];
                if (tie.Kind == RelationshipKind.ParentOf && to < from + 1)
                {
                    generation[tie.ToEntryId] = from + 1;
                    changed = true;
                }
                else if (tie.Kind != RelationshipKind.ParentOf && from != to)
                {
                    generation[tie.FromEntryId] = generation[tie.ToEntryId] = Math.Max(from, to);
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        // Rows count from the top: the earliest generation is row 0, whatever the loops did.
        int lowest = generation.Values.Min();
        int highest = Math.Min(generation.Values.Max(), lowest + members.Count);
        return generation.ToDictionary(g => g.Key, g => Math.Min(g.Value, highest) - lowest);
    }

    // Under its parents if it has some placed, else with its spouse, else in its turn.
    private static double SortKey(Guid id, Dictionary<Guid, double> wanted,
        Dictionary<Guid, List<Guid>> spouses, Dictionary<Guid, int> order)
    {
        if (!double.IsNaN(wanted[id]))
        {
            return wanted[id];
        }

        double? spouseWants = spouses.GetValueOrDefault(id)?
            .Select(s => wanted.GetValueOrDefault(s, double.NaN))
            .FirstOrDefault(x => !double.IsNaN(x));
        return spouseWants is double x && !double.IsNaN(x) ? x : order[id] * ColumnSpacing;
    }

    // Moves each married-in spouse right next to their partner (the first one met keeps their
    // place).
    private static List<Guid> KeepSpousesTogether(List<Guid> row,
        Dictionary<Guid, List<Guid>> spouses)
    {
        var result = new List<Guid>();
        var done = new HashSet<Guid>();
        foreach (Guid id in row)
        {
            if (!done.Add(id))
            {
                continue;
            }

            result.Add(id);
            foreach (Guid spouse in spouses.GetValueOrDefault(id) ?? [])
            {
                if (row.Contains(spouse) && done.Add(spouse))
                {
                    result.Add(spouse);
                }
            }
        }

        return result;
    }

    // Places a row left to right, each as near where it wants to be as the spacing allows, then
    // shifts the whole row so it sits as near as it can, on average, to where it wanted to be.
    private static void PlaceRow(List<Guid> row, Dictionary<Guid, double> wanted, double y,
        Dictionary<Guid, (double X, double Y)> placed)
    {
        var xs = new double[row.Count];
        for (int i = 0; i < row.Count; i++)
        {
            double want = double.IsNaN(wanted[row[i]])
                ? (i == 0 ? 0 : xs[i - 1] + ColumnSpacing)
                : wanted[row[i]];
            xs[i] = i == 0 ? want : Math.Max(want, xs[i - 1] + ColumnSpacing);
        }

        List<int> anchored = [.. Enumerable.Range(0, row.Count)
            .Where(i => !double.IsNaN(wanted[row[i]]))];
        double shift = anchored.Count == 0
            ? -(xs[0] + xs[^1]) / 2  // A row with nothing above it is centered on 0
            : -anchored.Average(i => xs[i] - wanted[row[i]]);
        for (int i = 0; i < row.Count; i++)
        {
            placed[row[i]] = (xs[i] + shift, y);
        }
    }

    // The rest in a grid under the tree (or at the top, without one), centered like it, in an
    // order that walks along the ties so tied entries end up near each other.
    private static void PlaceOthers(Dictionary<Guid, int> order, List<Relationship> ties,
        HashSet<Guid> inFamily, Dictionary<Guid, (double X, double Y)> placed, int familyRows)
    {
        List<Guid> others = [.. order.Keys.Where(id => !inFamily.Contains(id))];
        if (others.Count == 0)
        {
            return;
        }

        var neighbors = others.ToDictionary(id => id, _ => new List<Guid>());
        foreach (Relationship tie in ties.Where(t => neighbors.ContainsKey(t.FromEntryId)
            && neighbors.ContainsKey(t.ToEntryId)))
        {
            neighbors[tie.FromEntryId].Add(tie.ToEntryId);
            neighbors[tie.ToEntryId].Add(tie.FromEntryId);
        }

        var walk = new List<Guid>();
        var seen = new HashSet<Guid>();
        foreach (Guid start in others)
        {
            var queue = new Queue<Guid>();
            if (seen.Add(start))
            {
                queue.Enqueue(start);
            }

            while (queue.Count > 0)
            {
                Guid id = queue.Dequeue();
                walk.Add(id);
                foreach (Guid next in neighbors[id].OrderBy(n => order[n]).Where(seen.Add))
                {
                    queue.Enqueue(next);
                }
            }
        }

        int columns = (int)Math.Ceiling(Math.Sqrt(walk.Count));
        double middle = placed.Count == 0 ? 0 : (placed.Values.Min(p => p.X)
            + placed.Values.Max(p => p.X)) / 2;
        double top = familyRows == 0 ? 0 : (familyRows + 0.5) * RowSpacing;
        for (int i = 0; i < walk.Count; i++)
        {
            int row = i / columns, column = i % columns;
            int inRow = Math.Min(columns, walk.Count - row * columns);
            placed[walk[i]] = (middle + (column - (inRow - 1) / 2.0) * ColumnSpacing,
                top + row * RowSpacing);
        }
    }
}
