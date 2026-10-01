using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Names and markers for the bodies in the system view (VISION.md REN-02), and clicking a body
/// to fly to it. Bodies too small to see (far away, or at true scale) get a dot so they can
/// still be found and clicked; other bodies are labelled when seen from afar.
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

    private static readonly Color _starColor = new(1.0f, 0.85f, 0.45f);
    private static readonly Color _planetColor = new(0.55f, 0.75f, 1.0f);
    private static readonly Color _moonColor = new(0.8f, 0.8f, 0.85f);
    private static readonly Color _labelColor = new(0.92f, 0.94f, 0.98f);

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
    }

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
