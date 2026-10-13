using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Rendering;

namespace NothicWorlds.Controls;

/// <summary>
/// The roads laid up close (VISION.md BOD-13): each stone highway on its grade along its way
/// (<see cref="RoadProfile"/>), the ground cut and banked to it (<see cref="RoadCarving"/>,
/// with the rivers' channels in <see cref="GroundCarving"/>), so the ground tiles, the fine
/// strips, walking, and plants all follow it; and each dirt path's worn strands
/// (<see cref="DirtPath"/>, <see cref="PathStrands"/>), lying on the ground, with its fords,
/// wooden bridges, and boardwalks shaping the ground as highways do. The strips, ribbons, and
/// bridges near the eye are worked out with the rivers' banks (StartBanks), and the highways'
/// tunnels with them (the coarser ground at their portals sunk out of sight, as
/// RoadCarving does it); walkers who come in by a portal walk on their road (Walked). An old
/// road is laid as a highway is, and a cart track as a dirt path.
/// </summary>
public partial class FirstPersonMode
{
    // The roads' profiles and the ground they shape, and the dirt paths' strands, worked out
    // for the ways, ground, rivers, and plants (where marsh is) they were worked out from
    // (they change rarely).
    private (RoadsFor For, RoadCarving? Carving, PathStrands? Strands)? _roadProfiles;

    // The roads being laid on a worker (LayRoads), and what for: the ground tiles wait for
    // them, so none is built twice.
    private (RoadsFor For, Task<LaidRoads> Laying)? _pendingRoads;

    // The dirt paths' strands (none: null).
    private PathStrands? Strands => _roadProfiles?.Strands;

    // Each highway's tunnels, by its road's id, as spans of meters along its way (the
    // minimap dashes them).
    private Dictionary<Guid, List<(double From, double To)>> _tunnelSpans = [];

    // The tunnels' portals near the eye (walkers don't stand on the coarser ground sunk there).
    private TunnelPortal[] _portals = [];

    // The ground walked on at a direction, in meters, where the shaped ground there is
    // `groundMeters`: in a tunnel, its road, if the ground under the eye is nearer that than
    // the hill over it (walkers come in by a portal; over the hill, they stay on it), so a
    // tunnel's walls are as steep as a cliff to walkers inside (VISION.md BOD-13).
    private double Walked(GroundCarving carving, Vector3D direction, double groundMeters) =>
        carving.TunnelFloorMeters(direction) is double floor
        && Math.Abs(_groundMeters - floor) < Math.Abs(_groundMeters - groundMeters)
            ? floor
            : groundMeters;

    // Starts working out the roads' profiles and the dirt paths' strands again, on a worker,
    // if the ways, the ground, the rivers they cross, or where marsh is have changed since
    // they were (or are being) laid (TakeInRoads takes them in).
    private void KeepCarving(PlanetSurface globe, Body body)
    {
        var wanted = new RoadsFor(Session?.RoadsOn(body.Id) ?? [], globe.ReliefVersion,
            _profiles?.Carving, PlantCoverVersion(body));
        if ((_pendingRoads?.For ?? _roadProfiles?.For) is { } known && known.Same(wanted))
        {
            return;
        }

        (TerrainGrid, TerrainType[]) cover = _plantCover!.Value;
        _pendingRoads = (wanted, Task.Run(() => LayRoads(globe, body.RadiusKm, wanted, cover)));
    }

    // Takes in the roads once they're laid (KeepCarving), and with them the ground carving
    // everything up close follows (after the rivers': RiversOn). When the roads change,
    // every tile's built again.
    private void TakeInRoads(PlanetSurface globe, Body body)
    {
        if (_pendingRoads is not ({ } wanted, { IsCompleted: true } laying))
        {
            return;
        }

        _pendingRoads = null;
        LaidRoads laid = new(null, null, []);
        if (laying.Exception is { } error)
        {
            // Without them the ground goes on unshaped, and the log says why.
            GD.PushError($"Couldn't lay the roads: {error.GetBaseException()}");
        }
        else
        {
            laid = laying.Result;
        }

        bool hadRoads = _roadProfiles?.Carving is not null || _roadProfiles?.Strands is not null;
        _roadProfiles = (wanted, laid.Carving, laid.Strands);
        _tunnelSpans = laid.Tunnels;
        _carving = GroundCarving.Of(wanted.Rivers, laid.Carving);
        FadeRoughness(globe, _carving, body.RadiusKm);
        if (hadRoads || laid.Carving is not null || laid.Strands is not null)
        {
            _tileHeights++;
            _banksStale = true;
        }
    }

    // Lays the body's highways and dirt paths (on workers, one road each) over its smooth
    // ground, crossing the rivers it's laid `for`, marsh where `cover` has it: the ground they
    // shape (the highways and the dirt paths' crossings; none: null), the dirt paths' strands
    // (none: null), and the highways' tunnels (for _tunnelSpans). On a worker.
    private static LaidRoads LayRoads(PlanetSurface globe, double radiusKm, RoadsFor @for,
        (TerrainGrid Terrain, TerrainType[] Types) cover)
    {
        IReadOnlyList<RoadCourseShown> courses = @for.Courses;
        RiverCarving? rivers = @for.Rivers;
        double radiusMeters = radiusKm * 1000;
        double Ground(Vector3D direction) =>
            globe.SmoothGroundLiftAt(direction, radiusKm) * radiusMeters;
        (TerrainGrid terrain, TerrainType[] types) = cover;
        HashSet<int> marshCodes = [.. types.Where(type => type.Plants == PlantCover.Marsh)
            .Select(type => type.Code)];
        bool IsMarsh(Vector3D direction) =>
            marshCodes.Count > 0 && marshCodes.Contains(terrain.CodeAt(direction));

        var highways = new RoadProfile?[courses.Count];
        var paths = new DirtPath?[courses.Count];
        Parallel.For(0, courses.Count, i =>
        {
            highways[i] = RoadProfile.For(courses[i], radiusKm, Ground, rivers);
            paths[i] = DirtPath.For(courses[i], radiusKm, Ground, rivers, IsMarsh);
        });

        List<RoadProfile> laid = [.. highways.OfType<RoadProfile>(),
            .. paths.OfType<DirtPath>().SelectMany(path => path.Crossings)];
        PathStrands strands = PathStrands.For(paths.OfType<DirtPath>(), radiusKm);
        Dictionary<Guid, List<(double From, double To)>> tunnels = [];
        for (int i = 0; i < courses.Count; i++)
        {
            if (highways[i] is { Tunnels.Count: > 0 } highway)
            {
                tunnels[courses[i].RoadId] = [.. highway.Tunnels.Select(tunnel =>
                    (highway.AlongMeters[tunnel.First], highway.AlongMeters[tunnel.Last]))];
            }
        }

        return new LaidRoads(laid.Count == 0 ? null : RoadCarving.For(laid, radiusKm),
            strands.IsEmpty ? null : strands, tunnels);
    }

    // Whether the body's roads have been worked out again since they were laid (or began to
    // be) here, so the ground near them is due to be built again.
    private bool RoadsChangedOn(Body body) =>
        Session is { } session && (_pendingRoads?.For ?? _roadProfiles?.For) is { } known
        && !ReferenceEquals(known.Courses, session.RoadsOn(body.Id));

    // What the roads are laid for: the ways, the ground's version, the rivers they cross, and
    // where marsh is.
    private sealed record RoadsFor(IReadOnlyList<RoadCourseShown> Courses, int Relief,
        RiverCarving? Rivers, long Plants)
    {
        // Whether the roads laid for this are those for `other` (the ways and rivers are the
        // same objects as long as they haven't changed).
        public bool Same(RoadsFor other) => ReferenceEquals(Courses, other.Courses)
            && Relief == other.Relief && ReferenceEquals(Rivers, other.Rivers)
            && Plants == other.Plants;
    }

    // The roads laid (LayRoads): the ground they shape, the dirt paths' strands, and each
    // highway's tunnels, as spans along its way, by its road's id.
    private sealed record LaidRoads(RoadCarving? Carving, PathStrands? Strands,
        Dictionary<Guid, List<(double From, double To)>> Tunnels);
}
