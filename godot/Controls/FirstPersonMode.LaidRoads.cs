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
/// bridges near the eye are worked out with the rivers' banks (StartBanks).
/// </summary>
public partial class FirstPersonMode
{
    // The roads' profiles and the ground they shape, and the dirt paths' strands, worked out
    // for the ways, ground, rivers, and plants (where marsh is) they were worked out from
    // (they change rarely).
    private (IReadOnlyList<RoadCourseShown> Courses, int Relief, RiverCarving? Rivers,
        long Plants, RoadCarving? Carving, PathStrands? Strands)? _roadProfiles;

    // The dirt paths' strands (none: null).
    private PathStrands? Strands => _roadProfiles?.Strands;

    // Works out the roads' profiles and the dirt paths' strands again if the ways, the ground,
    // the rivers they cross, or where marsh is have changed, and with them the ground carving
    // everything up close follows (after the rivers': RiversOn). When the roads change, every
    // tile's built again.
    private void KeepCarving(PlanetSurface globe, Body body)
    {
        RiverCarving? rivers = _profiles?.Carving;
        IReadOnlyList<RoadCourseShown> courses = Session?.RoadsOn(body.Id) ?? [];
        long plants = PlantCoverVersion(body);
        bool roadsChanged = false;
        if (_roadProfiles is not var (known, relief, under, plantsKnown, _, _)
            || !ReferenceEquals(known, courses) || relief != globe.ReliefVersion
            || !ReferenceEquals(under, rivers) || plantsKnown != plants)
        {
            (RoadCarving? carving, PathStrands? strands) = LayRoads(globe, body, courses,
                rivers);
            bool hadRoads = _roadProfiles?.Carving is not null
                || _roadProfiles?.Strands is not null;
            _roadProfiles = (courses, globe.ReliefVersion, rivers, plants, carving, strands);
            roadsChanged = hadRoads || carving is not null || strands is not null;
        }

        if (roadsChanged || _carving is null
            || !ReferenceEquals(_carving.Rivers, rivers is { IsEmpty: false } ? rivers : null))
        {
            _carving = GroundCarving.Of(rivers, _roadProfiles!.Value.Carving);
            FadeRoughness(globe, _carving, body.RadiusKm);
        }

        if (roadsChanged)
        {
            _tileHeights++;
            _banksStale = true;
        }
    }

    // Lays the body's highways and dirt paths (on workers, one road each) over its smooth
    // ground, crossing `rivers`: the ground they shape (the highways and the dirt paths'
    // crossings; none: null) and the dirt paths' strands (none: null).
    private (RoadCarving? Carving, PathStrands? Strands) LayRoads(PlanetSurface globe,
        Body body, IReadOnlyList<RoadCourseShown> courses, RiverCarving? rivers)
    {
        double radiusKm = body.RadiusKm, radiusMeters = radiusKm * 1000;
        double Ground(Vector3D direction) =>
            globe.SmoothGroundLiftAt(direction, radiusKm) * radiusMeters;
        (TerrainGrid terrain, TerrainType[] types) = _plantCover!.Value;
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
        return (laid.Count == 0 ? null : RoadCarving.For(laid, radiusKm),
            strands.IsEmpty ? null : strands);
    }

    // Whether the body's roads have been worked out again since they were laid here (so the
    // ground near them is due to be built again).
    private bool RoadsChangedOn(Body body) =>
        Session is { } session && _roadProfiles is { } known
        && !ReferenceEquals(known.Courses, session.RoadsOn(body.Id));
}
