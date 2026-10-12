using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Rendering;

namespace NothicWorlds.Controls;

/// <summary>
/// The stone highways up close (VISION.md BOD-13; owner's choices, 2026-10-12): each laid on
/// its grade along its way (<see cref="RoadProfile"/>), the ground cut and banked to it
/// (<see cref="RoadCarving"/>, with the rivers' channels in <see cref="GroundCarving"/>), so the
/// ground tiles, the fine strips, walking, and plants all follow it. The strips and bridges
/// near the eye are worked out with the rivers' banks (StartBanks).
/// </summary>
public partial class FirstPersonMode
{
    // Each highway's profile along its way and the ground they shape, worked out for the
    // ways, ground, and rivers they were worked out from (they change rarely).
    private (IReadOnlyList<RoadCourseShown> Courses, int Relief, RiverCarving? Rivers,
        RoadCarving? Carving)? _roadProfiles;

    // Works out the highways' profiles again if the ways, the ground, or the rivers they cross
    // have changed, and with them the ground carving everything up close follows (after the
    // rivers': RiversOn). When the roads change, every tile's built again.
    private void KeepCarving(PlanetSurface globe, Body body)
    {
        RiverCarving? rivers = _profiles?.Carving;
        IReadOnlyList<RoadCourseShown> courses = Session?.RoadsOn(body.Id) ?? [];
        bool roadsChanged = false;
        if (_roadProfiles is not var (known, relief, under, _)
            || !ReferenceEquals(known, courses) || relief != globe.ReliefVersion
            || !ReferenceEquals(under, rivers))
        {
            double radiusKm = body.RadiusKm, radiusMeters = radiusKm * 1000;
            var profiles = new RoadProfile?[courses.Count];
            Parallel.For(0, courses.Count, i => profiles[i] = RoadProfile.For(courses[i],
                radiusKm, direction => globe.SmoothGroundLiftAt(direction, radiusKm)
                    * radiusMeters, rivers));
            List<RoadProfile> roads = [.. profiles.OfType<RoadProfile>()];
            bool hadRoads = _roadProfiles?.Carving is not null;
            RoadCarving? carving = roads.Count == 0 ? null : RoadCarving.For(roads, radiusKm);
            _roadProfiles = (courses, globe.ReliefVersion, rivers, carving);
            roadsChanged = hadRoads || carving is not null;
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

    // Whether the body's roads have been worked out again since the highways' profiles were
    // (so the ground near them is due to be built again).
    private bool RoadsChangedOn(Body body) =>
        Session is { } session && _roadProfiles is { } known
        && !ReferenceEquals(known.Courses, session.RoadsOn(body.Id));
}
