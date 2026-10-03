using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Marks eclipses on the moon's orbit (VISION.md EVT-01; owner's request): where the moon is
/// at an eclipse's peak. Only each moon's previous and next eclipse are marked, and only while
/// they're within one trip of the moon around its planet from now (owner's request: a ring of
/// every eclipse in the year was too much). Solar eclipses are dark disks ringed in gold (the
/// moon in front of the star); lunar ones are dark red disks (the moon in shadow).
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
    private bool _shown = true;
    private MarkerPlan? _plan;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>Whether the eclipse markers show (View ▸ Eclipse Markers).</summary>
    public bool ShowMarkers
    {
        get => _shown;
        set
        {
            _shown = value;
            _overlay.QueueRedraw();
        }
    }

    /// <summary>The system view, for where each body is drawn.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The camera, for projecting points onto the screen.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>The toolbar: the markers hide whenever it does (calibrating, cutting).</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    // The markers that may show: each moon's previous and next eclipse, for one timeline and
    // number of eclipses already peaked (so it changes only when an eclipse peaks). Whether
    // each is within an orbit of now is checked as it's drawn.
    private sealed record MarkerPlan(EclipseTimeline Timeline, int Peaked, Marker[] Markers);

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
        if (!Visible || !_shown || Session?.SelectedEclipses is not EclipseTimeline timeline)
        {
            return;
        }

        Font font = _overlay.GetThemeDefaultFont();
        Rect2 onScreen = _overlay.GetRect().Grow(OffScreenMarginPixels);
        double now = Session.TimeDays;
        foreach (Marker marker in CurrentPlan(timeline).Markers)
        {
            if (Math.Abs(marker.PeakDays - now) > marker.Moon.Orbit!.PeriodDays
                || !System!.Layout.TryGetValue(marker.Parent.Id, out DisplayBody parentPlace))
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

    // The markers that may show now: the cached ones, unless the timeline changed or another
    // eclipse peaked.
    private MarkerPlan CurrentPlan(EclipseTimeline timeline)
    {
        double now = Session!.TimeDays;
        int peaked = PeakedBy(timeline, now);
        if (_plan is { } cached && cached.Timeline == timeline && cached.Peaked == peaked)
        {
            return cached;
        }

        IReadOnlyList<Body> bodies = Session.World.Bodies;
        var previous = new Dictionary<Guid, Marker>();
        var next = new Dictionary<Guid, Marker>();
        foreach (Eclipse eclipse in timeline.Eclipses)
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
            bool isPast = eclipse.PeakDays <= now;
            string label = $"{(isPast ? "Previous" : "Next")}: {EclipseText.Short(eclipse)}";
            var marker = new Marker(moon, parent, eclipse.PeakDays,
                eclipse.Kind == EclipseKind.Solar, label);
            if (isPast)
            {
                previous[moon.Id] = marker;  // In order, so the last one wins.
            }
            else
            {
                next.TryAdd(moon.Id, marker);
            }
        }

        _plan = new MarkerPlan(timeline, peaked, [.. previous.Values, .. next.Values]);
        return _plan;
    }

    // How many eclipses have peaked by a time (a loop, so it allocates nothing).
    private static int PeakedBy(EclipseTimeline timeline, double timeDays)
    {
        IReadOnlyList<Eclipse> eclipses = timeline.Eclipses;
        int count = 0;
        while (count < eclipses.Count && eclipses[count].PeakDays <= timeDays)
        {
            count++;
        }

        return count;
    }
}
