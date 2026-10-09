using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public class GroundClearanceTests
{
    [Fact]
    public void Nearest_OverFlatGround_IsTheHeightAboveIt()
    {
        double nearest = GroundClearance.Nearest(500, [(100, 0), (500, 0)]);

        Assert.Equal(500, nearest, 9);
    }

    [Fact]
    public void Nearest_FlyingTowardAHillside_IsTheHillsideNotTheGroundBelow()
    {
        // The bug: 500 m up, with a hillside 20 m away rising to the eye's height, the near
        // distance followed the 500 m below and cut the hillside away.
        double nearest = GroundClearance.Nearest(500, [(20, 500)]);

        Assert.Equal(20, nearest, 9);
    }

    [Fact]
    public void Nearest_OnAFortyFiveDegreeSlope_FindsTheSlopeAhead()
    {
        // Ground rising as fast as it goes out: the nearest point is at half the height.
        double nearest = GroundClearance.Nearest(2, [(1, 1)]);

        Assert.Equal(Math.Sqrt(2), nearest, 9);
    }

    [Fact]
    public void Nearest_WithGroundAboveTheEye_CountsItsDistance()
    {
        double nearest = GroundClearance.Nearest(10, [(3, 14)]);

        Assert.Equal(5, nearest, 9);
    }

    [Theory]
    [InlineData(1, 1, false)]        // 45°
    [InlineData(1, 1.3, true)]       // ~52°
    [InlineData(0.5, 30, true)]      // A cliff
    [InlineData(1, -30, false)]      // Going down
    public void TooSteep_IsOnlyClimbsOverFiftyDegrees(double run, double rise, bool steep)
    {
        Assert.Equal(steep, GroundClearance.TooSteep(run, rise));
    }
}
