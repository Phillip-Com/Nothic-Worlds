using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Measurement;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Rendering;
using NothicWorlds.UI;

namespace NothicWorlds.Controls;

/// <summary>
/// A sense of scale while standing (VISION.md REN-06; owner's choices, 2026-10-09): a
/// rangefinder, reading the distance to the ground (or water) at the middle of the view and
/// under the mouse, and the pinned places on the body marked on the compass with their
/// distances.
/// </summary>
public partial class FirstPersonMode
{
    // How far the rangefinder looks, in meters: past the farthest ground drawn standing.
    private const double RangeReachMeters = 200_000;

    // How often a line of sight is measured, in seconds; the middle and the mouse take turns,
    // each measured on a worker (a long look past the ground takes a few hundred samples).
    private const double RangeSeconds = 0.05;

    // Rough ground finer than this share of a sample's distance is left out of the
    // rangefinder's samples: far off it's smaller than its steps (and cheaper without).
    private const double RangeDetailShare = 0.01;

    private double _sinceRanged = double.PositiveInfinity;
    private bool _rangeMouseNext;
    private string? _mouseRange;
    private Task<GroundRange.Hit?>? _pendingRange;  // A line of sight being measured ...
    private bool _pendingMouse;                     // ... under the mouse (else the middle)
    private double _pendingEyeMeters;               // ... from this high above the base

    /// <summary>The journal's and timeline's pins, marked on the compass while standing.</summary>
    [Export] public PinMarkers? Pins { get; set; }

    // Shows a finished measurement, then sets off the next, the middle of the view and the
    // spot under the mouse taking turns. The middle's reading goes under the crosshair, the
    // mouse's in the hover readout.
    private void UpdateRanges(PlanetSurface globe, Body body,
        (Vector3D East, Vector3D North, Vector3D Up) frame, double delta)
    {
        if (_pendingRange is { IsCompleted: true } done)
        {
            _pendingRange = null;
            ShowRange(done);
        }

        _sinceRanged += delta;
        if (_pendingRange is not null || _sinceRanged < RangeSeconds)
        {
            return;
        }

        _sinceRanged = 0;
        _rangeMouseNext = !_rangeMouseNext;
        if (_flat is { Face: not FlatFace.Top })
        {
            _mouseRange = null;
            _hud!.SetRange("No range finding off the top face");
            return;
        }

        if (_rangeMouseNext && _dragging)
        {
            _mouseRange = null;
            return;
        }

        Vector2 at = _rangeMouseNext ? _mouse : GetViewport().GetVisibleRect().Size / 2;
        Vector3 ray = _camera!.ProjectRayNormal(at);
        var toward = new Vector3D(ray.X, ray.Y, ray.Z);
        double elevation = Math.Asin(Math.Clamp(toward.Dot(frame.Up), -1, 1));
        double bearing = Math.Atan2(toward.Dot(frame.East), toward.Dot(frame.North));
        double eyeMeters = _groundMeters + _heightMeters;
        double radiusMeters = _flat is null ? body.RadiusKm * 1000 : double.PositiveInfinity;

        // Under the water the line runs on to the bottom; above it, it stops at the surface.
        Func<double, double> surface = SurfaceToward(globe, body, bearing, !_underwater.Visible);
        _pendingMouse = _rangeMouseNext;
        _pendingEyeMeters = eyeMeters;
        _pendingRange = Task.Run(() =>
            GroundRange.Find(eyeMeters, elevation, radiusMeters, RangeReachMeters, surface));
    }

    // A measurement's reading: how far the ground is, and how much higher or lower than the
    // eye; under the crosshair, past the reach when nothing was met.
    private void ShowRange(Task<GroundRange.Hit?> done)
    {
        string? text;
        if (done.Exception is AggregateException failed)
        {
            GD.PushError($"The rangefinder failed: {failed.InnerException?.Message}");
            text = "Range unavailable";
        }
        else if (done.Result is GroundRange.Hit hit)
        {
            double rise = hit.HeightMeters - _pendingEyeMeters;
            string height = Math.Abs(rise) < 0.5
                ? "level"
                : $"{UnitText.Format(Quantity.Length, Math.Abs(rise))} " +
                    (rise > 0 ? "higher" : "lower");
            text = $"{ShortDistance(hit.Meters)} · {height}";
        }
        else
        {
            text = null;
        }

        if (_pendingMouse)
        {
            _mouseRange = text is null || _dragging ? null : $"Range {text}";
        }
        else
        {
            _hud!.SetRange(text ?? $"Over {UnitText.Distance(RangeReachMeters / 1000)}");
        }
    }

    // How high the drawn ground, or the water over it, is in meters above the base, for a
    // distance over the ground from the eye along `bearing` (radians clockwise from north),
    // as GroundMetersToward has it, but with no rough ground finer than the rangefinder's
    // step there. It keeps its own copy of where the eye stands and the rivers' carving, so
    // it can run on a worker (as the ground tiles' heights do) while the eye moves on.
    private Func<double, double> SurfaceToward(PlanetSurface globe, Body body, double bearing,
        bool water)
    {
        double radiusKm = body.RadiusKm, radiusMeters = radiusKm * 1000;
        Vector3D spot = _spot;
        FlatSpot? flat = _flat;
        RiverCarving? carving = _profiles is { Carving: var some } ? some : null;
        return meters =>
        {
            double smallest = Math.Max(TerrainRoughness.FinestMeters, meters * RangeDetailShare);
            Vector3D direction;
            double lift;
            if (flat is FlatSpot from)
            {
                FlatSpot there = FlatWalk.Walk(from, bearing, meters / radiusMeters);
                if (there.Face != FlatFace.Top)
                {
                    return 0;  // The rim and underside are bare rock at the base
                }

                Vector3D point = FlatWalk.Point(there);
                direction = FlatDisc.DirectionFor(point);
                lift = FlatGroundAt(globe, point, radiusKm, smallest);
                if (carving is not null && !globe.IsCarved)
                {
                    lift = Math.Max(carving.Carve(direction, lift * radiusMeters) / radiusMeters,
                        PlanetSurface.FlatDeepestLift);
                }
            }
            else
            {
                direction = GlobeWalk.Walk(spot, bearing, meters / radiusMeters);
                lift = globe.GroundLiftAt(direction, radiusKm, smallest);
                if (carving is not null)
                {
                    lift = carving.Carve(direction, lift * radiusMeters) / radiusMeters;
                }
            }

            double ground = lift * radiusMeters;
            return water && globe.WaterRadiusAt(direction) is double level
                ? Math.Max(ground, (level - 1) * radiusMeters)
                : ground;
        };
    }

    // The pinned places on the body, nearest first, on the compass with their distances.
    private void ShowPins(Body body)
    {
        if (Pins is null)
        {
            return;
        }

        var marks = new List<(double Km, double Bearing, Color Color, string Label)>();
        foreach ((GeoCoordinate spot, Color color, string title) in Pins.PinsOn(body.Id))
        {
            SurfaceBearing.Toward? toward = _flat is FlatSpot flat
                ? SurfaceBearing.OnFlat(flat, spot, body.RadiusKm)
                : SurfaceBearing.OnGlobe(_spot, spot, body.RadiusKm);
            if (toward is SurfaceBearing.Toward way)
            {
                marks.Add((way.Km, way.BearingDegrees, color,
                    $"{title} · {ShortDistance(way.Km * 1000)}"));
            }
        }

        _hud!.SetPins(marks.OrderBy(mark => mark.Km)
            .Select(mark => (mark.Bearing, mark.Color, mark.Label)));
    }

    // A distance in meters for reading: in m or ft under one km or mile, then in km or miles
    // (to a tenth under ten).
    private static string ShortDistance(double meters)
    {
        double km = meters / 1000;
        double shown = UnitText.Shown(Quantity.Distance, km);
        return shown < 1
            ? UnitText.Format(Quantity.Length, meters)
            : UnitText.Format(Quantity.Distance, km, shown < 10 ? 1 : 0);
    }
}
