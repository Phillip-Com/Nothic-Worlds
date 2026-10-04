using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class RealmsTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(2, 25, 70)]
    [InlineData(5, 60, 200)]
    public void ARealm_RidesItsBranchTip_AsTheTreeTurns(int branch, double tilt, double lean)
    {
        (World world, Body tree, Body realm) = TreeWithRealm(branch);
        tree.AxialTiltDegrees = tilt;
        tree.AxialTiltDirectionDegrees = lean;
        Realms.Apply(world.Bodies);
        Vector3D tip = WorldTreeShape.Grow(tree.Tree!).BranchTips[branch];

        foreach (double time in new[] { 0.0, 10.0, 91.3, 200.0, 365.25, 1000.0 })
        {
            Vector3D expected = DrawnTip(tree, tip, time) * tree.RadiusKm;
            Vector3D actual = OrbitMath.OffsetFromParent(realm.Orbit!, time);
            Assert.True((expected - actual).Length < 1e-6 * tree.RadiusKm,
                $"At {time}: expected {expected}, got {actual}");
        }
    }

    [Fact]
    public void ARealmsYear_IsOneTurnOfTheTree()
    {
        (World world, Body tree, Body realm) = TreeWithRealm(0);

        Assert.Equal(tree.DayLengthHours / 24, realm.Orbit!.PeriodDays, 9);
    }

    [Fact]
    public void EditingTheTree_MovesItsRealms()
    {
        (World world, Body tree, Body realm) = TreeWithRealm(1);
        Orbit before = realm.Orbit!;

        tree.RadiusKm *= 2;
        tree.Tree = tree.Tree! with { Seed = 99 };
        Assert.True(Realms.Apply(world.Bodies));

        Assert.NotEqual(before, realm.Orbit);
        Assert.Equal(Realms.OrbitOnBranch(tree, 1), realm.Orbit);
        Assert.False(Realms.Apply(world.Bodies));  // Already up to date
    }

    [Fact]
    public void Realms_MustBeWorldsOnTheirTreesBranches_OneToABranch()
    {
        (World world, Body tree, Body realm) = TreeWithRealm(0);
        Assert.Null(Realms.Problem(world.Bodies));

        realm.Branch = tree.Tree!.Branches;  // No such branch
        Assert.NotNull(Realms.Problem(world.Bodies));

        realm.Branch = 0;
        Body second = Realm(world, tree, 0);  // Same branch
        Assert.NotNull(Realms.Problem(world.Bodies));

        second.Branch = 1;
        Assert.Null(Realms.Problem(world.Bodies));
        Body star = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        realm.Orbit = realm.Orbit! with { ParentId = star.Id };  // Not on a tree
        Assert.NotNull(Realms.Problem(world.Bodies));
    }

    [Fact]
    public void AHungRealm_CantBeMadeTheCenter()
    {
        (World world, _, Body realm) = TreeWithRealm(0);

        Assert.Empty(SystemHierarchy.MakeCenter(world.Bodies, realm.Id));
    }

    [Fact]
    public void ARealm_IsDrawnAtItsDrawnBranchTip()
    {
        (World world, Body tree, Body realm) = TreeWithRealm(3);
        Vector3D tip = WorldTreeShape.Grow(tree.Tree!).BranchTips[3];

        Dictionary<Guid, DisplayBody> layout =
            SystemLayout.At(world.Bodies, 50, SystemScale.Readable);

        Vector3D drawn = layout[realm.Id].Position - layout[tree.Id].Position;
        Vector3D expected = DrawnTip(tree, tip, 50);
        double cos = drawn.Dot(expected) / (drawn.Length * expected.Length);
        Assert.True(cos > 0.999999);
        Assert.Equal(expected.Length * layout[tree.Id].Radius + layout[realm.Id].Radius,
            drawn.Length, 6);
    }

    [Fact]
    public void LiftedOrbits_AreReversedByMakeCenter()
    {
        (World world, Body tree, _) = TreeWithRealm(0);
        Body moon = NewBodies.Moon(world.Bodies, tree);
        moon.Orbit = moon.Orbit! with { HeightKm = 5000, TiltDegrees = 20 };
        world.Bodies.Add(moon);
        Vector3D before = OrbitMath.OffsetFromParent(moon.Orbit, 12);

        Orbit reversed = SystemHierarchy.MakeCenter(world.Bodies, moon.Id)[tree.Id]!;

        Vector3D after = OrbitMath.OffsetFromParent(reversed, 12);
        Assert.True((after + before).Length < 1e-6);
    }

    // Where the tree's drawing puts a tip (in its radii): spun about its axis, then leaned by
    // its tilt toward its tilt direction (right-handed rotations, as Godot draws it).
    private static Vector3D DrawnTip(Body tree, Vector3D tip, double time)
    {
        Vector3D spun = tip.RotatedAroundY(BodyClock.SpinDegrees(tree, time));
        Vector3D lean = BodyOrientation.LeanDirection(tree);
        return Rotate(spun, Cross(new Vector3D(0, 1, 0), lean),
            double.DegreesToRadians(tree.AxialTiltDegrees));
    }

    private static Vector3D Cross(Vector3D a, Vector3D b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    // Rodrigues' rotation of v about a unit axis k by an angle.
    private static Vector3D Rotate(Vector3D v, Vector3D k, double angle)
    {
        if (angle == 0)
        {
            return v;
        }

        k *= 1 / k.Length;
        return v * Math.Cos(angle) + Cross(k, v) * Math.Sin(angle)
            + k * (k.Dot(v) * (1 - Math.Cos(angle)));
    }

    private static (World World, Body Tree, Body Realm) TreeWithRealm(int branch)
    {
        World world = World.CreateNew();
        Body star = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        Body tree = NewBodies.WorldTree(world.Bodies, star);
        world.Bodies.Add(tree);
        Body realm = Realm(world, tree, branch);
        return (world, tree, realm);
    }

    private static Body Realm(World world, Body tree, int branch)
    {
        var realm = new Body
        {
            Name = $"Realm {branch}",
            Branch = branch,
            Orbit = Realms.OrbitOnBranch(tree, branch),
        };
        world.Bodies.Add(realm);
        return realm;
    }
}
