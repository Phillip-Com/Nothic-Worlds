using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public sealed class StrokePathTests
{
    [Fact]
    public void ThePathsDistance_IsToItsNearestPoint_AlongTheWayOrAtAnEnd()
    {
        var path = new StrokePath(At(0, 0), At(0, 40));

        Assert.Equal(5, Degrees(path.DistanceTo(At(5, 20))), 4);  // Beside it
        Assert.Equal(0, Degrees(path.DistanceTo(At(0, 33))), 4);  // On it
        Assert.Equal(10, Degrees(path.DistanceTo(At(0, 50))), 4);  // Past the end
        Assert.Equal(3, Degrees(path.DistanceTo(At(0, -3))), 4);  // Before the start
    }

    [Fact]
    public void APointStroke_IsMeasuredFromItsPoint()
    {
        var path = new StrokePath(At(10, 10), At(10, 10));

        Assert.Equal(7, Degrees(path.DistanceTo(At(17, 10))), 4);
    }

    [Fact]
    public void ALongStroke_FollowsTheShortestWayRound()
    {
        var path = new StrokePath(At(0, -80), At(0, 170));  // 110° the short way, over 180°

        Assert.Equal(0, Degrees(path.DistanceTo(At(0, -150))), 4);
        Assert.True(Degrees(path.DistanceTo(At(0, 45))) > 100);
    }

    // Directions come from single-precision vectors, so angles match to about 1e-5°.
    private static double Degrees(double radians) => double.RadiansToDegrees(radians);

    private static Vector3D At(double latitude, double longitude)
    {
        System.Numerics.Vector3 direction =
            SphericalCoordinates.ToDirection(new GeoCoordinate(latitude, longitude));
        return new Vector3D(direction.X, direction.Y, direction.Z);
    }
}
