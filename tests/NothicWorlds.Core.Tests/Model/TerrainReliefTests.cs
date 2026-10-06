using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public class TerrainReliefTests
{
    private const byte Low = 1, High = 2;
    private static readonly Vector3D _faceMiddle = new(0, 0, 1);

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

    // How many cells the ground takes to rise (neither at its low nor its high height).
    private static int Rising(short[] profile) => profile.Count(h => h is > 200 and < 2_000);
}
