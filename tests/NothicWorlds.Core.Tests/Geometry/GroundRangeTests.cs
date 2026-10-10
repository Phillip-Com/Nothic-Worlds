using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public class GroundRangeTests
{
    private const double Flat = double.PositiveInfinity;

    [Fact]
    public void Find_LookingDownAtFlatGround_MeetsItWhereTheSlopeSays()
    {
        // 100 m up, looking 45° down: the ground is 100 m out, ~141 m along the line.
        GroundRange.Hit? hit = GroundRange.Find(100, -Math.PI / 4, Flat, 10_000, _ => 0);

        Assert.NotNull(hit);
        Assert.Equal(100 * Math.Sqrt(2), hit.Value.Meters, 3);
        Assert.Equal(100, hit.Value.AcrossMeters, 3);
        Assert.Equal(0, hit.Value.HeightMeters, 9);
    }

    [Fact]
    public void Find_LookingLevelAtACliff_MeetsTheCliffFace()
    {
        // Ground rising to 500 m at 2 km out, looked at level from 2 m up.
        GroundRange.Hit? hit = GroundRange.Find(2, 0, Flat, 10_000,
            across => across >= 2_000 ? 500 : 0);

        Assert.NotNull(hit);
        Assert.Equal(2_000, hit.Value.Meters, 3);
        Assert.Equal(500, hit.Value.HeightMeters, 9);
    }

    [Fact]
    public void Find_LookingUp_MissesWithinTheReach()
    {
        Assert.Null(GroundRange.Find(2, 0.1, Flat, 100_000, _ => 0));
    }

    [Fact]
    public void Find_LookingLevelOverAGlobe_MissesAsTheGroundCurvesAway()
    {
        // Level over a smooth globe the line never comes back down to the ground.
        Assert.Null(GroundRange.Find(2, 0, 6_371_000, 170_000, _ => 0));
    }

    [Fact]
    public void Find_LookingAtTheHorizonOfAGlobe_MeetsItAtTheHorizon()
    {
        // From 100 m up on Earth the horizon is ~35.7 km off, ~0.32° below level; a line
        // just under that meets the ground close to it.
        double radius = 6_371_000, eye = 100;
        double dip = Math.Acos(radius / (radius + eye));
        GroundRange.Hit? hit = GroundRange.Find(eye, -dip - 1e-5, radius, 170_000, _ => 0);

        Assert.NotNull(hit);
        double horizon = Math.Sqrt((radius + eye) * (radius + eye) - radius * radius);
        Assert.InRange(hit.Value.Meters, horizon * 0.9, horizon);
    }

    [Fact]
    public void Find_IsTheSameEachTime()
    {
        static double Hills(double across) => 300 * Math.Sin(across / 700) + 300;

        GroundRange.Hit? first = GroundRange.Find(650, -0.05, 6_371_000, 50_000, Hills);
        GroundRange.Hit? second = GroundRange.Find(650, -0.05, 6_371_000, 50_000, Hills);

        Assert.NotNull(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Find_WithNoReach_FindsNothing()
    {
        Assert.Null(GroundRange.Find(2, -1, Flat, 0, _ => 0));
    }

    [Fact]
    public void Point_OverAGlobe_DropsAwayBelowALevelLine()
    {
        // 10 km out level from the ground, the ground has fallen ~7.85 m below the line.
        (double across, double height) = GroundRange.Point(0, 0, 6_371_000, 10_000);

        Assert.Equal(10_000, across, 0);
        Assert.Equal(7.85, height, 2);
    }
}
