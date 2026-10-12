using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Rendering;
using NothicWorlds.UI;

namespace NothicWorlds.Controls;

/// <summary>
/// The roads on the minimap while standing (VISION.md BOD-13, REN-08; owner's request,
/// 2026-10-11): each road's way, as seen straight down from above the spot like the map's
/// picture, in its style's color (dirt paths dashed, as on the globe).
/// </summary>
public partial class FirstPersonMode
{
    // How wide each style's line is on the minimap, in pixels.
    private static float MapLineWidth(RoadStyle style) => style switch
    {
        RoadStyle.StoneHighway => 3,
        RoadStyle.OldRoad => 2.5f,
        _ => 2,
    };

    // Puts the body's roads on the minimap, which reaches `spanMeters` across.
    private void ShowRoadsOnMap(Body body, double spanMeters)
    {
        if (_hud is null || Session is null)
        {
            return;
        }

        IReadOnlyList<RoadCourseShown> ways = Session.RoadsOn(body.Id);
        Func<Vector3D, Vector2?>? toMap = ways.Count == 0 ? null
            : MapPlacer(spanMeters / 2 / (body.RadiusKm * 1000));
        var lines = new List<MinimapLine>();
        foreach (RoadCourseShown way in toMap is null ? [] : ways)
        {
            // Split where the way goes out of sight (round the far side of a globe).
            var run = new List<Vector2>();
            foreach (Vector3D point in way.Path)
            {
                if (toMap!(point) is Vector2 at)
                {
                    run.Add(at);
                    continue;
                }

                AddRun(lines, run, way.Style);
                run.Clear();
            }

            AddRun(lines, run, way.Style);
        }

        _hud.Minimap.SetLines(lines);
    }

    // Where a direction on the body lies on the minimap, -1 to 1 across (x east, y north),
    // half the map being `halfRadii` globe radii across; null where it can't be seen from
    // above (the far side of a globe; on a flat world, off its top face).
    private Func<Vector3D, Vector2?>? MapPlacer(double halfRadii)
    {
        if (_flat is FlatSpot flat)
        {
            if (flat.Face != FlatFace.Top)
            {
                return null;
            }

            (Vector3D flatEast, Vector3D flatNorth, _) = FlatWalk.Frame(flat);
            Vector3D here = FlatWalk.Point(flat);
            return direction =>
            {
                Vector3D offset = FlatDisc.TopPointFor(direction) - here;
                return new Vector2((float)(offset.Dot(flatEast) / halfRadii),
                    (float)(offset.Dot(flatNorth) / halfRadii));
            };
        }

        Vector3D up = _spot;
        (Vector3D east, Vector3D north) = GlobeWalk.Tangents(up);
        return direction => direction.Dot(up) <= 0 ? null
            : new Vector2((float)(direction.Dot(east) / halfRadii),
                (float)(direction.Dot(north) / halfRadii));
    }

    private static void AddRun(List<MinimapLine> lines, List<Vector2> run, RoadStyle style)
    {
        if (run.Count >= 2)
        {
            lines.Add(new MinimapLine([.. run], RoadRenderer.ColorOf(style),
                MapLineWidth(style), Dashed: style == RoadStyle.DirtPaths));
        }
    }
}
