using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class RiverBanksTests
{
    private const double RadiusKm = 6371;
    private const double MetersPerDegree = RadiusKm * 1000 * Math.PI / 180;

    // A river 100 m wide at its mouth running east along the equator over level ground 100 m
    // up, an eye beside it, and the coarser ground drawn a meter lower.
    private static readonly Vector3D _eye = WaterGround.At(10 / MetersPerDegree, 0.01);

    private static List<List<BankPoint[]>> Strips()
    {
        RiverProfile river = RiverProfile.For(
            RiverProfileTests.Drawn(0.1, WaterGround.At(0, -1), WaterGround.At(0, 1)),
            RadiusKm, _ => 100)!;
        RiverChannels channels = RiverChannels.Near([river], _eye, RadiusKm, 20_000, _ => 100);
        return RiverBanks.Strips(channels, _eye, RadiusKm, _ => 100, _ => 99);
    }

    [Fact]
    public void EachRow_RunsFromBedUpTheBanks_ToTheCoarserGroundAtItsEdges()
    {
        // The row nearest the eye, where the channel's fully carved.
        BankPoint[] row = Assert.Single(Strips())
            .MinBy(r => Angle(_eye, r[r.Length / 2].Direction))!;

        BankPoint middle = row[row.Length / 2];
        Assert.True(middle.Meters < 99);
        Assert.Equal(1, middle.Shaped);
        Assert.InRange(row[0].Meters, 99, 99.5);  // Just over the coarser ground
        Assert.InRange(row[^1].Meters, 99, 99.5);
        Assert.Equal(0, row[0].Shaped);
        for (int i = row.Length / 2 + 1; i < row.Length - 2; i++)
        {
            Assert.True(row[i].Meters >= row[i - 1].Meters - 1e-9, $"point {i} dips");
        }
    }

    [Fact]
    public void TheStrip_CoversOnlyTheCarvedStretch()
    {
        List<BankPoint[]> strip = Assert.Single(Strips());
        double reach = RiverChannels.CarveReachMeters(50) + 500;

        Assert.All(strip, row => Assert.True(
            Angle(_eye, row[row.Length / 2].Direction) * MetersPerDegree < reach));
    }

    private static double Angle(Vector3D a, Vector3D b) =>
        double.RadiansToDegrees(Math.Acos(Math.Clamp(a.Dot(b), -1, 1)));
}
