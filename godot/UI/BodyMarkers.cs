using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Names and markers for the bodies in the system view (VISION.md REN-02), and clicking a body
/// to fly to it. Bodies too small to see (far away, or at true scale) get a dot so they can
/// still be found and clicked; other bodies are labelled when seen from afar. The selected
/// body's solstices and equinoxes are marked on the orbit that shows its year (VISION.md
/// CAL-03; owner's request): its own, its planet's for a moon, or the star's in a
/// planet-centered system.
/// </summary>
/// <remarks>
/// A click on another body selects it only if the mouse barely moved, so dragging to orbit the
/// camera never changes the selection. Must come after the camera in the scene: later nodes
/// get unhandled input first, and this one only watches (it never blocks the camera).
/// </remarks>
public partial class BodyMarkers : CanvasLayer
{
    private const float DotRadius = 4.0f;
    private const float DotBelowPixels = 6.0f;
    private const float LabelBelowPixels = 60.0f;
    private const float ClickSlopPixels = 5.0f;
    private const float GrabPixels = 10.0f;
    private const float SeasonMarkerRadius = 5.0f;

    // How far past the screen edge a season marker may sit and still be drawn (its label can
    // reach in).
    private const float OffScreenMarginPixels = 200.0f;

    private static readonly Color _planetColor = new(0.55f, 0.75f, 1.0f);
    private static readonly Color _moonColor = new(0.8f, 0.8f, 0.85f);
    private static readonly Color _cometColor = new(0.7f, 0.95f, 0.95f);
    private static readonly Color _labelColor = new(0.92f, 0.94f, 0.98f);

    // Each event's marker takes the color of the northern season it begins.
    private static readonly Color _springColor = new(0.55f, 0.9f, 0.5f);
    private static readonly Color _summerColor = new(1.0f, 0.82f, 0.35f);
    private static readonly Color _autumnColor = new(0.95f, 0.55f, 0.3f);
    private static readonly Color _winterColor = new(0.6f, 0.8f, 1.0f);

    // Meteor shower markers: a small streak, like a meteor.
    private static readonly Color _meteorColor = new(0.75f, 0.95f, 1.0f);
    private static readonly Vector2 _streak = new(-11, -7);

    private Control _overlay = null!;
    private bool _showSeasons = true;
    private bool _showShowers = true;
    private Vector2? _pressedAt;
    private Guid? _pressedBody;

    // The season markers being shown, kept until the timeline or the current season changes,
    // so drawing every frame allocates nothing (allocations add up to garbage-collection
    // stutters).
    private SeasonMarkerPlan? _seasonPlan;

    // The meteor shower markers being shown. A shower happens at the same point of the orbit
    // every year, so they only change with the showers themselves.
    private ShowerMarkerPlan? _showerPlan;

    // Reused corner lists for drawing diamonds.
    private readonly Vector2[] _diamond = new Vector2[4];
    private readonly Vector2[] _diamondOutline = new Vector2[5];

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    // The markers for one year of seasons: which orbit they sit on and what each one shows.
    private sealed record SeasonMarkerPlan(
        SeasonTimeline Timeline,
        SeasonEvent? Latest,
        Body? Traveller,
        Body? Parent,
        IReadOnlyList<SeasonMarker> Markers);

    private readonly record struct SeasonMarker(
        double TimeDays, bool IsSolstice, Color Color, string Label);

    // The markers for a year of meteor showers: which orbit they sit on, and each one's peak
    // time and label.
    private sealed record ShowerMarkerPlan(
        MeteorShowerTimeline Timeline,
        Body? Traveller,
        Body? Parent,
        IReadOnlyList<(double TimeDays, string Label)> Markers);

    /// <summary>The system view, for where each body is drawn.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>
    /// Whether the meteor shower markers show (View ▸ Meteor Shower Markers; VISION.md EVT-02).
    /// </summary>
    public bool ShowShowerMarkers
    {
        get => _showShowers;
        set
        {
            _showShowers = value;
            _overlay?.QueueRedraw();
        }
    }

    /// <summary>Whether the solstice and equinox markers show (View ▸ Season Markers).</summary>
    public bool ShowSeasonMarkers
    {
        get => _showSeasons;
        set
        {
            _showSeasons = value;
            _overlay.QueueRedraw();
        }
    }

    /// <summary>The camera, for projecting bodies onto the screen.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>The toolbar: markers hide whenever it does, and messages go to it.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    public override void _Ready()
    {
        Layer = 0;  // Over the 3D view, under the toolbar and panels.
        _overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.Draw += DrawMarkers;
        AddChild(_overlay);

        if (Session is null || System is null || Camera is null)
        {
            GD.PushError("BodyMarkers needs a world session, system view, and camera.");
            SetProcessUnhandledInput(false);
            return;
        }

        System.Placed += _overlay.QueueRedraw;
        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += () => Visible = Toolbar.Visible;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } click)
        {
            return;
        }

        if (click.Pressed)
        {
            _pressedAt = click.Position;
            _pressedBody = BodyAt(click.Position);
            return;
        }

        if (_pressedAt is Vector2 start && _pressedBody is Guid body
            && start.DistanceTo(click.Position) <= ClickSlopPixels)
        {
            _ = SelectAsync(body);
        }

        _pressedAt = null;
        _pressedBody = null;
    }

    private async Task SelectAsync(Guid bodyId)
    {
        if (await Session!.SelectBodyAsync(bodyId) is string warning)
        {
            Toolbar?.ShowWarning(warning);
        }
    }

    // The body (other than the selected one) under a screen position: its globe, or its dot.
    private Guid? BodyAt(Vector2 position)
    {
        Guid? best = null;
        float bestDistance = float.MaxValue;
        foreach ((Body body, Vector2 center, float radius) in OnScreen())
        {
            float distance = center.DistanceTo(position);
            if (body.Id != Session!.SelectedBodyId
                && distance <= Math.Max(radius, GrabPixels) && distance < bestDistance)
            {
                best = body.Id;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void DrawMarkers()
    {
        // In the local view the camera looks straight down at the ground: markers out in
        // space would only clutter the map (VISION.md REN-04).
        if (!Visible || Camera!.IsLocalView)
        {
            return;
        }

        Font font = _overlay.GetThemeDefaultFont();
        foreach ((Body body, Vector2 center, float radius) in OnScreen())
        {
            Color color = body.Kind switch
            {
                BodyKind.Star => BodyAppearance.StarColor(body.Appearance.StarType).ToGodot(),
                BodyKind.Moon => _moonColor,
                BodyKind.Comet => _cometColor,
                _ => _planetColor,
            };
            if (radius < DotBelowPixels)
            {
                _overlay.DrawCircle(center, DotRadius + 1.5f, Colors.Black);
                _overlay.DrawCircle(center, DotRadius, color);
            }

            if (radius < LabelBelowPixels && body.Id != Session!.SelectedBodyId)
            {
                Vector2 at = center + new Vector2(Math.Max(radius, DotRadius) + 4, 4);
                _overlay.DrawString(font, at + Vector2.One, body.Name, modulate: Colors.Black);
                _overlay.DrawString(font, at, body.Name, modulate: _labelColor);
            }
        }

        if (_showSeasons)
        {
            DrawSeasonMarkers(font);
        }

        if (_showShowers)
        {
            DrawShowerMarkers(font);
        }
    }

    // Where on the orbit that shows the selected body's year each meteor shower peaks.
    private void DrawShowerMarkers(Font font)
    {
        if (CurrentShowerPlan() is not { Traveller: Body traveller, Parent: Body parent } plan
            || !System!.Layout.TryGetValue(parent.Id, out DisplayBody parentPlace))
        {
            return;
        }

        foreach ((double timeDays, string label) in plan.Markers)
        {
            Vector3D onOrbit = parentPlace.Position + SystemLayout.OrbitPoint(
                traveller, parent, timeDays, System.DisplayScale);
            Vector3 scenePosition = System.ToScene(onOrbit);
            if (Camera!.IsPositionBehind(scenePosition))
            {
                continue;
            }

            // Off-screen markers are skipped, as for the seasons.
            Vector2 center = Camera.UnprojectPosition(scenePosition);
            if (!_overlay.GetRect().Grow(OffScreenMarginPixels).HasPoint(center))
            {
                continue;
            }

            _overlay.DrawLine(center, center + _streak, Colors.Black, 4.0f);
            _overlay.DrawLine(center, center + _streak, _meteorColor, 2.0f);
            _overlay.DrawCircle(center, SeasonMarkerRadius - 1 + 1.5f, Colors.Black);
            _overlay.DrawCircle(center, SeasonMarkerRadius - 1, _meteorColor);
            Vector2 at = center + new Vector2(SeasonMarkerRadius + 4, SeasonMarkerRadius + 12);
            _overlay.DrawString(font, at + Vector2.One, label, modulate: Colors.Black);
            _overlay.DrawString(font, at, label, modulate: _meteorColor);
        }
    }

    // The shower markers to show now: the cached ones, unless the showers changed. Null while
    // they're still being worked out.
    private ShowerMarkerPlan? CurrentShowerPlan()
    {
        if (Session!.SelectedMeteorShowers is not MeteorShowerTimeline timeline)
        {
            return null;
        }

        if (_showerPlan is { } cached && cached.Timeline == timeline)
        {
            return cached;
        }

        IReadOnlyList<Body> bodies = Session.World.Bodies;
        Body? traveller = Seasons.OrbitShowingYear(bodies, Session.SelectedBody);
        Body? parent = traveller?.Orbit is Orbit orbit
            ? bodies.FirstOrDefault(b => b.Id == orbit.ParentId)
            : null;
        (double, string)[] markers = [.. timeline.Between(0, timeline.YearDays)
            .Where(shower => shower.PeakDays >= 0 && shower.PeakDays < timeline.YearDays)
            .Select(shower => (shower.PeakDays, MarkerLabel(shower, bodies)))];
        _showerPlan = new ShowerMarkerPlan(timeline, traveller, parent, markers);
        return _showerPlan;
    }

    private static string MarkerLabel(MeteorShower shower, IReadOnlyList<Body> bodies)
    {
        string? comet = bodies.FirstOrDefault(b => b.Id == shower.CometId)?.Name;
        return comet is null ? "Meteor shower" : $"{comet} meteors";
    }

    // The selected body's year of solstices and equinoxes, on the orbit that shows its year.
    // Equinoxes are round, solstices are diamonds.
    private void DrawSeasonMarkers(Font font)
    {
        SeasonMarkerPlan plan = CurrentSeasonPlan();
        if (plan.Traveller is not Body traveller || plan.Parent is not Body parent
            || !System!.Layout.TryGetValue(parent.Id, out DisplayBody parentPlace))
        {
            return;
        }

        foreach (SeasonMarker marker in plan.Markers)
        {
            Vector3D onOrbit = parentPlace.Position + SystemLayout.OrbitPoint(
                traveller, parent, marker.TimeDays, System.DisplayScale);
            Vector3 scenePosition = System.ToScene(onOrbit);
            if (Camera!.IsPositionBehind(scenePosition))
            {
                continue;
            }

            // Skip markers off the screen: a point nearly beside the camera projects millions
            // of pixels away, and the engine stalls drawing shapes there.
            Vector2 center = Camera.UnprojectPosition(scenePosition);
            if (!_overlay.GetRect().Grow(OffScreenMarginPixels).HasPoint(center))
            {
                continue;
            }

            DrawSeasonMarker(center, marker.IsSolstice, marker.Color);
            Vector2 at = center + new Vector2(SeasonMarkerRadius + 4, -SeasonMarkerRadius);
            _overlay.DrawString(font, at + Vector2.One, marker.Label, modulate: Colors.Black);
            _overlay.DrawString(font, at, marker.Label, modulate: marker.Color);
        }
    }

    // The markers to show now: the cached ones, unless the world, selection, or season changed.
    private SeasonMarkerPlan CurrentSeasonPlan()
    {
        SeasonTimeline timeline = Session!.SelectedSeasons;
        SeasonEvent? latest = timeline.LatestEvent(Session.TimeDays);
        if (_seasonPlan is { } cached && cached.Timeline == timeline && cached.Latest == latest)
        {
            return cached;
        }

        IReadOnlyList<Body> bodies = Session.World.Bodies;
        Body? traveller = Seasons.OrbitShowingYear(bodies, Session.SelectedBody);
        Body? parent = traveller?.Orbit is Orbit orbit
            ? bodies.FirstOrDefault(b => b.Id == orbit.ParentId)
            : null;
        SeasonMarker[] markers = [.. timeline.YearAround(Session.TimeDays).Select(e =>
            new SeasonMarker(e.TimeDays, IsSolstice(e.Kind), SeasonColor(e.Kind),
                SeasonText.ShortName(e.Kind)))];
        _seasonPlan = new SeasonMarkerPlan(timeline, latest, traveller, parent, markers);
        return _seasonPlan;
    }

    // The color of the northern season an event begins.
    private static Color SeasonColor(SeasonEventKind kind)
    {
        return Seasons.SeasonsAfter(kind).Northern switch
        {
            Season.Spring => _springColor,
            Season.Summer => _summerColor,
            Season.Autumn => _autumnColor,
            _ => _winterColor,
        };
    }

    private void DrawSeasonMarker(Vector2 center, bool diamond, Color color)
    {
        if (!diamond)
        {
            _overlay.DrawCircle(center, SeasonMarkerRadius + 1.5f, Colors.Black);
            _overlay.DrawCircle(center, SeasonMarkerRadius, color);
            return;
        }

        float r = SeasonMarkerRadius + 1;
        _diamond[0] = center + new Vector2(0, -r);
        _diamond[1] = center + new Vector2(r, 0);
        _diamond[2] = center + new Vector2(0, r);
        _diamond[3] = center + new Vector2(-r, 0);
        _diamond.CopyTo(_diamondOutline, 0);
        _diamondOutline[4] = _diamond[0];
        _overlay.DrawColoredPolygon(_diamond, color);
        _overlay.DrawPolyline(_diamondOutline, Colors.Black, 1.5f);
    }

    private static bool IsSolstice(SeasonEventKind kind) =>
        kind is SeasonEventKind.NorthernSummerSolstice or SeasonEventKind.NorthernWinterSolstice
            or SeasonEventKind.Midwinter;

    // Each body in front of the camera: where its center is on screen and its radius in pixels.
    private IEnumerable<(Body Body, Vector2 Center, float Radius)> OnScreen()
    {
        if (Session is null || System is null || Camera is null)
        {
            yield break;
        }

        foreach (Body body in Session.World.Bodies)
        {
            if (!System.Layout.TryGetValue(body.Id, out DisplayBody place))
            {
                continue;
            }

            Vector3 center = System.ToScene(place.Position);
            if (Camera.IsPositionBehind(center))
            {
                continue;
            }

            Vector2 screen = Camera.UnprojectPosition(center);
            Vector3 edge = center + Camera.GlobalBasis.X * (float)place.Radius;
            float radius = screen.DistanceTo(Camera.UnprojectPosition(edge));
            yield return (body, screen, radius);
        }
    }
}
