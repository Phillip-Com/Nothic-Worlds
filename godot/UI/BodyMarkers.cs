using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
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

    private static readonly Color _starColor = new(1.0f, 0.85f, 0.45f);
    private static readonly Color _planetColor = new(0.55f, 0.75f, 1.0f);
    private static readonly Color _moonColor = new(0.8f, 0.8f, 0.85f);
    private static readonly Color _labelColor = new(0.92f, 0.94f, 0.98f);

    // Each event's marker takes the color of the northern season it begins.
    private static readonly Color _springColor = new(0.55f, 0.9f, 0.5f);
    private static readonly Color _summerColor = new(1.0f, 0.82f, 0.35f);
    private static readonly Color _autumnColor = new(0.95f, 0.55f, 0.3f);
    private static readonly Color _winterColor = new(0.6f, 0.8f, 1.0f);

    private Control _overlay = null!;
    private Vector2? _pressedAt;
    private Guid? _pressedBody;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, for where each body is drawn.</summary>
    [Export] public SystemView? System { get; set; }

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
        if (!Visible)
        {
            return;
        }

        Font font = _overlay.GetThemeDefaultFont();
        foreach ((Body body, Vector2 center, float radius) in OnScreen())
        {
            Color color = body.Kind switch
            {
                BodyKind.Star => _starColor,
                BodyKind.Moon => _moonColor,
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

        DrawSeasonMarkers(font);
    }

    // The selected body's year of solstices and equinoxes, on the orbit that shows its year.
    // Equinoxes are round, solstices are diamonds.
    private void DrawSeasonMarkers(Font font)
    {
        IReadOnlyList<Body> bodies = Session!.World.Bodies;
        IReadOnlyList<SeasonEvent> year = Session.SelectedSeasons.YearAround(Session.TimeDays);
        if (year.Count == 0
            || Seasons.OrbitShowingYear(bodies, Session.SelectedBody) is not Body traveller
            || traveller.Orbit is not Orbit orbit
            || bodies.FirstOrDefault(b => b.Id == orbit.ParentId) is not Body parent
            || !System!.Layout.TryGetValue(parent.Id, out DisplayBody parentPlace))
        {
            return;
        }

        foreach (SeasonEvent seasonEvent in year)
        {
            Vector3D onOrbit = parentPlace.Position + SystemLayout.OrbitPoint(
                traveller, parent, seasonEvent.TimeDays, System.DisplayScale);
            Vector3 scenePosition = System.ToScene(onOrbit);
            if (Camera!.IsPositionBehind(scenePosition))
            {
                continue;
            }

            Vector2 center = Camera.UnprojectPosition(scenePosition);
            Color color = Seasons.SeasonsAfter(seasonEvent.Kind).Northern switch
            {
                Season.Spring => _springColor,
                Season.Summer => _summerColor,
                Season.Autumn => _autumnColor,
                _ => _winterColor,
            };
            DrawSeasonMarker(center, IsSolstice(seasonEvent.Kind), color);
            Vector2 at = center + new Vector2(SeasonMarkerRadius + 4, -SeasonMarkerRadius);
            string label = SeasonText.ShortName(seasonEvent.Kind);
            _overlay.DrawString(font, at + Vector2.One, label, modulate: Colors.Black);
            _overlay.DrawString(font, at, label, modulate: color);
        }
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
        Vector2[] corners =
        [
            center + new Vector2(0, -r), center + new Vector2(r, 0),
            center + new Vector2(0, r), center + new Vector2(-r, 0),
        ];
        _overlay.DrawColoredPolygon(corners, color);
        _overlay.DrawPolyline([.. corners, corners[0]], Colors.Black, 1.5f);
    }

    private static bool IsSolstice(SeasonEventKind kind) =>
        kind is SeasonEventKind.NorthernSummerSolstice or SeasonEventKind.NorthernWinterSolstice;

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
