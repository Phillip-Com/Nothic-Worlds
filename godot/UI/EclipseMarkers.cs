using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Marks the selected body's coming eclipses on the moon's orbit (VISION.md EVT-01; owner's
/// request): where the moon is at each eclipse's peak. Solar eclipses are dark disks ringed in
/// gold (the moon in front of the star); lunar ones are dark red disks (the moon in shadow).
/// </summary>
/// <remarks>
/// The orbit is drawn around the planet's current position, as the orbit line is, so the
/// markers sit on that line. Like the season markers, the list is kept until it changes, and
/// markers off the screen are skipped (a point nearly beside the camera projects millions of
/// pixels away, and the engine stalls drawing there).
/// </remarks>
public partial class EclipseMarkers : CanvasLayer
{
    private const float MarkerRadius = 5.0f;
    private const float OffScreenMarginPixels = 200.0f;

    private static readonly Color _solarRing = new(1.0f, 0.78f, 0.3f);
    private static readonly Color _solarDisk = new(0.08f, 0.08f, 0.1f);
    private static readonly Color _lunarDisk = new(0.62f, 0.16f, 0.12f);
    private static readonly Color _labelColor = new(0.92f, 0.94f, 0.98f);

    private Control _overlay = null!;
    private MarkerPlan? _plan;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, for where each body is drawn.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The camera, for projecting points onto the screen.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>The toolbar: the markers hide whenever it does (calibrating, cutting).</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    // The markers being shown, for one eclipse timeline from the first eclipse not yet over.
    private sealed record MarkerPlan(EclipseTimeline Timeline, int First, Marker[] Markers);

    // One eclipse: the moon whose orbit it sits on, that orbit's parent, and what it shows.
    private readonly record struct Marker(
        Body Moon, Body Parent, double PeakDays, bool IsSolar, string Label);

    public override void _Ready()
    {
        Layer = 0;  // Over the 3D view, under the toolbar and panels.
        _overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.Draw += DrawMarkers;
        AddChild(_overlay);

        if (Session is null || System is null || Camera is null)
        {
            GD.PushError("EclipseMarkers needs a world session, system view, and camera.");
            return;
        }

        System.Placed += _overlay.QueueRedraw;
        Session.EclipsesReady += _overlay.QueueRedraw;
        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += () => Visible = Toolbar.Visible;
        }
    }

    private void DrawMarkers()
    {
        if (!Visible || Session?.SelectedEclipses is not EclipseTimeline timeline)
        {
            return;
        }

        Font font = _overlay.GetThemeDefaultFont();
        Rect2 onScreen = _overlay.GetRect().Grow(OffScreenMarginPixels);
        foreach (Marker marker in CurrentPlan(timeline).Markers)
        {
            if (!System!.Layout.TryGetValue(marker.Parent.Id, out DisplayBody parentPlace))
            {
                continue;
            }

            Vector3D onOrbit = parentPlace.Position + SystemLayout.OrbitPoint(
                marker.Moon, marker.Parent, marker.PeakDays, System.DisplayScale);
            Vector3 scenePosition = System.ToScene(onOrbit);
            if (Camera!.IsPositionBehind(scenePosition))
            {
                continue;
            }

            Vector2 center = Camera.UnprojectPosition(scenePosition);
            if (!onScreen.HasPoint(center))
            {
                continue;
            }

            _overlay.DrawCircle(center, MarkerRadius + 1.5f,
                marker.IsSolar ? _solarRing : Colors.Black);
            _overlay.DrawCircle(center, MarkerRadius, marker.IsSolar ? _solarDisk : _lunarDisk);
            Vector2 at = center + new Vector2(MarkerRadius + 4, MarkerRadius + 10);
            _overlay.DrawString(font, at + Vector2.One, marker.Label, modulate: Colors.Black);
            _overlay.DrawString(font, at, marker.Label, modulate: _labelColor);
        }
    }

    // The markers to show now: the cached ones, unless the timeline changed or an eclipse ended.
    private MarkerPlan CurrentPlan(EclipseTimeline timeline)
    {
        int first = FirstNotOver(timeline, Session!.TimeDays);
        if (_plan is { } cached && cached.Timeline == timeline && cached.First == first)
        {
            return cached;
        }

        IReadOnlyList<Body> bodies = Session.World.Bodies;
        var markers = new List<Marker>();
        foreach (Eclipse eclipse in timeline.Upcoming(Session.TimeDays))
        {
            Body? blocker = bodies.FirstOrDefault(b => b.Id == eclipse.BlockerId);
            Body? shadowed = bodies.FirstOrDefault(b => b.Id == eclipse.ShadowedId);
            if (blocker is null || shadowed is null)
            {
                continue;
            }

            // The marker goes on the orbit of whichever of the two circles the other.
            (Body moon, Body parent) = blocker.Orbit?.ParentId == shadowed.Id
                ? (blocker, shadowed)
                : (shadowed, blocker);
            markers.Add(new Marker(moon, parent, eclipse.PeakDays,
                eclipse.Kind == EclipseKind.Solar, EclipseText.Short(eclipse)));
        }

        _plan = new MarkerPlan(timeline, first, [.. markers]);
        return _plan;
    }

    // The index of the first eclipse not over by a time (a loop, so it allocates nothing).
    private static int FirstNotOver(EclipseTimeline timeline, double timeDays)
    {
        IReadOnlyList<Eclipse> eclipses = timeline.Eclipses;
        for (int i = 0; i < eclipses.Count; i++)
        {
            if (eclipses[i].EndDays >= timeDays)
            {
                return i;
            }
        }

        return eclipses.Count;
    }
}
