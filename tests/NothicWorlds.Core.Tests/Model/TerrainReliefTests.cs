using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public class TerrainReliefTests
{
    private const byte Low = 1, High = 2;
    private static readonly Vector3D _faceMiddle = new(0, 0, 1);

    [Fact]
    public void ChangedTiles_CountsOnlyThePaintingThatChanged()
    {
        TerrainGrid before = TerrainGrid.Empty.Paint(_faceMiddle, 10, Low);

        Assert.Equal(0, TerrainRelief.ChangedTiles(before, before));
        int dab = TerrainRelief.ChangedTiles(before, before.Paint(_faceMiddle, 0.2, High));
        Assert.InRange(dab, 1, 4);
        Assert.True(TerrainRelief.ChangedTiles(TerrainGrid.Empty, before) > dab);
    }

    [Fact]
    public void Unpainted_IsFlat()
    {
        Assert.True(TerrainRelief.BaseHeights(TerrainGrid.Empty, Types(0, 0)).IsEmpty);
    }

    [Fact]
    public void AWideArea_TakesItsTypesHeight()
    {
        TerrainGrid terrain = TerrainGrid.Empty.Paint(_faceMiddle, 20, High);

        HeightGrid heights = TerrainRelief.BaseHeights(terrain, Types(0, 0));

        Assert.Equal(2_000, heights.HeightAt(_faceMiddle));
        Assert.Equal(0, heights.HeightAt(new Vector3D(0, 0, -1)));  // Unpainted, far off
    }

    [Fact]
    public void ACliff_JumpsStraightFromOneHeightToTheOther()
    {
        HeightGrid heights = Island(edge: 1);
        short[] profile = Profile(heights);

        int jump = Array.FindIndex(profile, h => h == 2_000);
        Assert.True(jump > 0);
        Assert.Equal(200, profile[jump - 1]);   // Low ground right up to the line...
        Assert.All(profile[..jump], h => Assert.Equal(200, h));
        Assert.All(profile[jump..], h => Assert.Equal(2_000, h));  // ...and high beyond it
    }

    [Fact]
    public void AGentleEdge_RisesSmoothly_ThroughHalfway()
    {
        HeightGrid heights = Island(edge: 0);
        short[] profile = Profile(heights);

        Assert.Equal(200, profile[0]);
        Assert.Equal(2_000, profile[^1]);
        for (int i = 1; i < profile.Length; i++)
        {
            Assert.True(profile[i] >= profile[i - 1], $"falls at {i}");
            Assert.True(profile[i] - profile[i - 1] < 250, $"jumps at {i}");
        }

        Assert.Contains(profile, h => Math.Abs(h - 1_100) < 150);  // Halfway, at the line
    }

    [Fact]
    public void TheSteeperEdge_Wins()
    {
        // The low ground is gentle, the high ground steep: the slope is as narrow as steep.
        short[] steep = Profile(Island(edge: 0, highEdge: 0.6));
        short[] gentle = Profile(Island(edge: 0));

        Assert.True(Rising(steep) < Rising(gentle) / 2);
        Assert.Equal(Rising(Profile(Island(edge: 0.6, highEdge: 0))), Rising(steep));
    }

    [Fact]
    public void AcrossAFacesEdge_TheGroundIsContinuous()
    {
        // A wide area centered where the +X and +Z faces meet, gently edged.
        var corner = new Vector3D(1, 0, 1);
        TerrainGrid terrain = TerrainGrid.Empty.Paint(corner, 25, High);
        HeightGrid heights = TerrainRelief.BaseHeights(terrain, Types(0, 0));

        // Along the equator from inside the area, over the faces' edge, and out past it.
        double previous = heights.SampleAt(corner);
        for (double degrees = 0; degrees <= 40; degrees += 0.05)
        {
            double angle = double.DegreesToRadians(45 + degrees);
            double height = heights.SampleAt(new Vector3D(Math.Sin(angle), 0, Math.Cos(angle)));
            Assert.True(Math.Abs(height - previous) < 250, $"jumps at {degrees}°");
            previous = height;
        }
    }

    [Fact]
    public void Update_MatchesWorkingItAllOut()
    {
        IReadOnlyList<TerrainType> types = Types(0.2, 0.5);
        TerrainGrid before = TerrainGrid.Empty.Paint(_faceMiddle, 10, Low);
        HeightGrid previous = TerrainRelief.BaseHeights(before, types);

        // A stroke across the edge of the low ground, and one over a face's edge.
        TerrainGrid after = before
            .PaintStroke(new Vector3D(0.1, 0, 1), new Vector3D(0.3, 0.1, 1), 3, High)
            .PaintStroke(new Vector3D(1, 0.2, 1), new Vector3D(1, 0.25, 0.9), 2, High);

        HeightGrid updated = TerrainRelief.Update(previous, before, after, types);

        Assert.True(updated.HasSameCells(TerrainRelief.BaseHeights(after, types)));
    }

    [Fact]
    public void Rework_MatchesWorkingItAllOut_AndKeepsTilesFarFromTheChangedType()
    {
        IReadOnlyList<TerrainType> before = Types(0, 0.5);
        TerrainGrid terrain = TerrainGrid.Empty
            .Paint(_faceMiddle, 10, Low)
            .Paint(new Vector3D(0, 1, 0), 5, High);  // Far away, on another face
        HeightGrid previous = TerrainRelief.BaseHeights(terrain, before);
        IReadOnlyList<TerrainType> after = [before[0], before[1] with { HeightMeters = 3_000 }];

        HeightGrid reworked = TerrainRelief.Rework(previous, terrain, before, after);

        Assert.True(reworked.HasSameCells(TerrainRelief.BaseHeights(terrain, after)));
        Assert.DoesNotContain(4, reworked.FacesChangedFrom(previous));  // The low ground's face
    }

    [Fact]
    public void Shaped_GivenWhatCameBefore_SharesWhatDidntChange()
    {
        HeightGrid ground = Island(edge: 0.5);
        HeightGrid sculpted = HeightGrid.Empty.Raise(_faceMiddle, _faceMiddle, 15, 300);
        HeightGrid first = TerrainRelief.Shaped(ground, sculpted);
        HeightGrid touched = sculpted.Raise(new Vector3D(0, 1, 0), new Vector3D(0, 1, 0), 1, 50);

        HeightGrid second = TerrainRelief.Shaped(ground, touched, (ground, sculpted, first));

        Assert.True(second.HasSameCells(TerrainRelief.Shaped(ground, touched)));
        Assert.DoesNotContain(4, second.FacesChangedFrom(first));
    }

    [Fact]
    public void Shaped_AddsTheSculptingOnTop_WithinWhatAHeightHolds()
    {
        HeightGrid ground = TerrainRelief.BaseHeights(
            TerrainGrid.Empty.Paint(_faceMiddle, 20, High), Types(0, 0));
        var aside = new Vector3D(0.15, 0, 1);  // Also on the high ground
        HeightGrid sculpted = HeightGrid.Empty
            .Raise(aside, aside, 1, 500)
            .Raise(_faceMiddle, _faceMiddle, 1, 32_000);

        HeightGrid shaped = TerrainRelief.Shaped(ground, sculpted);

        Assert.InRange(shaped.HeightAt(aside), 2_490, 2_500);
        Assert.Equal(HeightGrid.MaxHeightMeters, shaped.HeightAt(_faceMiddle));
        Assert.Same(sculpted, TerrainRelief.Shaped(HeightGrid.Empty, sculpted));
    }

    [Fact]
    public void Flatten_OnShapedGround_LevelsWhatsSeen()
    {
        HeightGrid ground = Island(edge: 0);
        Vector3D[] path = [_faceMiddle, new Vector3D(0.12, 0, 1)];

        HeightGrid sculpted = HeightGrid.Empty.Flatten(path, 2, 1_000, 1, under: ground);
        HeightGrid seen = TerrainRelief.Shaped(ground, sculpted);

        Assert.InRange(seen.HeightAt(new Vector3D(0.05, 0, 1)), 999, 1_001);
        Assert.InRange(seen.HeightAt(new Vector3D(0.1, 0, 1)), 999, 1_001);
    }

    [Fact]
    public void SampleSteepAt_StandsACliffUp_AndLeavesGentleSlopesAlone()
    {
        const double metersPerCell = 9_800;  // An Earth-sized world
        short[] cliff = Profile(Island(edge: 1));
        int jump = Array.FindIndex(cliff, h => h == 2_000);
        int row = HeightGrid.FaceSize / 2, column = HeightGrid.FaceSize / 2 - 120 + jump;

        // Across the cliff's cell gap, in tenths of a cell: the even blend climbs throughout,
        // the steep one is flat on either side with the wall in the middle.
        HeightGrid cliffs = Island(edge: 1);
        double[] even = Across(cliffs, column, row, (h, d) => h.SampleAt(d));
        double[] steep = Across(cliffs, column, row, (h, d) => h.SampleSteepAt(d, metersPerCell));
        Assert.True(even[2] > 400);                    // The even blend has begun to climb...
        Assert.InRange(steep[2], 199, 201);            // ...the steep one is still at the foot
        Assert.InRange(steep[8], 1_999, 2_001);        // and at the top soon after the middle.

        HeightGrid gentle = Island(edge: 0);
        Assert.Equal(Across(gentle, column, row, (h, d) => h.SampleAt(d)),
            Across(gentle, column, row, (h, d) => h.SampleSteepAt(d, metersPerCell)));
    }

    [Fact]
    public void Variation_GivesATypePeaksAndDips_WithinItsRange()
    {
        TerrainGrid terrain = TerrainGrid.Empty.Paint(_faceMiddle, 20, High);
        IReadOnlyList<TerrainType> types =
            [Types(0, 0)[0], Types(0, 0)[1] with { VariationMeters = 1_000, FeatureSizeKm = 60 }];

        HeightGrid heights = TerrainRelief.BaseHeights(terrain, types, 6_371, seed: 7);
        short[] row = Profile(heights);

        Assert.All(row, h => Assert.InRange(h, 1_000, 3_000));
        Assert.True(row.Max() - row.Min() > 400, "it should rise and fall");
        Assert.True(Neighbors(row).Average() < (row.Max() - row.Min()) / 6.0,
            "it should rise and fall over many cells, not jump about");
    }

    [Fact]
    public void Variation_IsTheSameEveryTime_AndDiffersFromBodyToBody()
    {
        TerrainGrid terrain = TerrainGrid.Empty.Paint(_faceMiddle, 20, High);
        IReadOnlyList<TerrainType> types = [Types(0, 0)[1] with { VariationMeters = 600 }];

        HeightGrid first = TerrainRelief.BaseHeights(terrain, types, 6_371, seed: 7);
        HeightGrid again = TerrainRelief.BaseHeights(terrain, types, 6_371, seed: 7);
        HeightGrid other = TerrainRelief.BaseHeights(terrain, types, 6_371, seed: 8);

        Assert.True(first.HasSameCells(again));
        Assert.False(first.HasSameCells(other));
    }

    [Fact]
    public void Variation_MeetsAGentleNeighborWithoutASeam()
    {
        // Two types at the same height and variation, with different features: where they
        // meet, the ground should be no rougher than either is on its own.
        TerrainType a = Types(0, 0)[0] with
        {
            HeightMeters = 1_000,
            VariationMeters = 500,
            FeatureSizeKm = 120,
        };
        TerrainType b = Types(0, 0)[1] with
        {
            HeightMeters = 1_000,
            VariationMeters = 500,
            FeatureSizeKm = 50,
        };
        TerrainGrid both =
            TerrainGrid.Empty.Paint(_faceMiddle, 20, Low).Paint(_faceMiddle, 5, High);
        TerrainGrid allA = TerrainGrid.Empty.Paint(_faceMiddle, 20, Low);
        TerrainGrid allB = TerrainGrid.Empty.Paint(_faceMiddle, 20, High);

        int Roughest(TerrainGrid terrain) =>
            Neighbors(Profile(TerrainRelief.BaseHeights(terrain, [a, b], 6_371, seed: 3))).Max();

        Assert.True(Roughest(both) <= Math.Max(Roughest(allA), Roughest(allB)) * 1.2,
            "no seam where the types meet");
    }

    [Fact]
    public void Rework_OfAVariation_MatchesWorkingItAllOut()
    {
        IReadOnlyList<TerrainType> before = Types(0, 0.3);
        TerrainGrid terrain = TerrainGrid.Empty
            .Paint(_faceMiddle, 10, Low)
            .Paint(_faceMiddle, 3, High);
        HeightGrid previous = TerrainRelief.BaseHeights(terrain, before, 3_000, seed: 1);
        IReadOnlyList<TerrainType> after =
            [before[0], before[1] with { VariationMeters = 700, FeatureSizeKm = 20 }];

        HeightGrid reworked = TerrainRelief.Rework(previous, terrain, before, after, 3_000, 1);

        Assert.True(reworked.HasSameCells(
            TerrainRelief.BaseHeights(terrain, after, 3_000, seed: 1)));
    }

    [Fact]
    public void Noise_IsLevelWithoutVariation_AndStaysWithinIt()
    {
        var place = new Vector3D(0.3, 0.4, 0.866);
        Assert.Equal(0, TerrainNoise.Offset(place, 6_371, 0, 50, 20, 1));
        for (int i = 0; i < 200; i++)
        {
            var direction = new Vector3D(Math.Sin(i * 0.37), Math.Cos(i * 0.11), 0.5);
            direction *= 1 / direction.Length;
            double offset = TerrainNoise.Offset(direction, 6_371, 1_500, 40, 20, 9);
            Assert.InRange(offset, -1_500, 1_500);
        }
    }

    [Fact]
    public void TransitionCells_RunsFromTheGentlestWidthToACliff()
    {
        Assert.Equal(TerrainRelief.GentlestWidthCells, TerrainRelief.TransitionCells(0, 0));
        Assert.Equal(0, TerrainRelief.TransitionCells(0, 1));
        Assert.Equal(TerrainRelief.TransitionCells(0.5, 0), TerrainRelief.TransitionCells(0, 0.5));
    }

    // Low ground (200 m) with high ground (2,000 m) painted in its middle.
    private static HeightGrid Island(double edge, double? highEdge = null)
    {
        TerrainGrid terrain = TerrainGrid.Empty
            .Paint(_faceMiddle, 20, Low)
            .Paint(_faceMiddle, 5, High);
        return TerrainRelief.BaseHeights(terrain, Types(edge, highEdge ?? edge));
    }

    private static IReadOnlyList<TerrainType> Types(double lowEdge, double highEdge) =>
    [
        new(Low, "Low", new RgbColor(0, 0, 0), HeightMeters: 200, Edge: lowEdge),
        new(High, "High", new RgbColor(0, 0, 0), HeightMeters: 2_000, Edge: highEdge),
    ];

    // Heights along the +Z face's middle row, from well out in the low ground to the middle.
    private static short[] Profile(HeightGrid heights)
    {
        int row = HeightGrid.FaceSize / 2;
        return [.. Enumerable.Range(HeightGrid.FaceSize / 2 - 120, 121)
            .Select(column => heights.HeightAt(new CubeCell(4, column, row)))];
    }

    // Heights from the middle of the cell before `column` to the middle of `column`, in
    // tenths of a cell, along the +Z face's row `row`.
    private static double[] Across(HeightGrid heights, int column, int row,
        Func<HeightGrid, Vector3D, double> sample) =>
        [.. Enumerable.Range(0, 11).Select(i =>
            sample(heights, CubeSphere.Direction(4,
                (column - 0.5 + i / 10.0) / HeightGrid.FaceSize,
                (row + 0.5) / HeightGrid.FaceSize)))];

    // The biggest step between neighboring values.
    private static IEnumerable<int> Neighbors(short[] row) =>
        row.Zip(row.Skip(1), (a, b) => Math.Abs(b - a));

    // How many cells the ground takes to rise (neither at its low nor its high height).
    private static int Rising(short[] profile) => profile.Count(h => h is > 200 and < 2_000);
}
