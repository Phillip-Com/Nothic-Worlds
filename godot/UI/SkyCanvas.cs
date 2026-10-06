using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;

namespace NothicWorlds.UI;

/// <summary>
/// The night sky laid flat, for drawing constellations (VISION.md REN-07): around the sky left
/// to right and from straight up (top) to straight down, as a world map lays out a globe. Every
/// star of the world's field is drawn, sized by brightness, with the constellations' lines.
/// The wheel zooms around the mouse; right- or middle-dragging pans. Clicks are reported as a
/// star, a line of the chosen constellation, or empty sky; the page decides what they do.
/// </summary>
public partial class SkyCanvas : Control
{
    private const float StarPickPixels = 9.0f;
    private const float LinePickPixels = 5.0f;
    private const int ArcSegments = 24;
    private const double MinDegreesPerPixel = 0.01;
    private const double ZoomStep = 1.25;

    private static readonly Color _background = new(0.02f, 0.025f, 0.05f);
    private static readonly Color _gridColor = new(0.3f, 0.4f, 0.6f, 0.18f);
    private static readonly Color _lineColor = new(0.45f, 0.6f, 0.95f, 0.75f);
    private static readonly Color _chosenColor = new(1.0f, 0.85f, 0.3f);
    private static readonly Color _nameColor = new(0.7f, 0.8f, 1.0f, 0.85f);

    private IReadOnlyList<StarField.Star> _stars = [];
    private Dictionary<int, StarField.Star> _byId = [];
    private int? _seed;

    // The view: the sky's direction at the middle of the canvas, and how much sky a pixel is.
    private double _centerLongitude;
    private double _centerLatitude;
    private double _degreesPerPixel = 0.25;
    private bool _panning;
    private Vector2 _mouse;

    /// <summary>Raised when a star is clicked, with its id.</summary>
    public event Action<int>? StarClicked;

    /// <summary>Raised when a line of the chosen constellation is clicked.</summary>
    public event Action<StarLink>? LineClicked;

    /// <summary>Raised when a click hits neither a star nor a chosen line.</summary>
    public event Action? EmptyClicked;

    /// <summary>The constellations to draw.</summary>
    public IReadOnlyList<Constellation> Constellations { get; set; } = [];

    /// <summary>The constellation being drawn, shown brighter, or null.</summary>
    public Guid? ChosenId { get; set; }

    /// <summary>The star a line will start from, if one has been picked.</summary>
    public int? PendingStar { get; set; }

    /// <summary>How many stars the sky has.</summary>
    public int StarCount => _stars.Count;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        FocusMode = FocusModeEnum.All;
        Resized += QueueRedraw;
    }

    /// <summary>
    /// Shows the stars from <paramref name="seed"/> (made again only for a new seed).
    /// </summary>
    public void ShowSky(int seed)
    {
        if (seed != _seed)
        {
            _seed = seed;
            _stars = StarField.Stars(seed);
            _byId = _stars.ToDictionary(star => star.Id);
        }

        QueueRedraw();
    }

    /// <summary>Zooms out to show the whole sky.</summary>
    public void FitView()
    {
        _centerLongitude = 0;
        _centerLatitude = 0;
        _degreesPerPixel = Math.Max(360.0 / Math.Max(Size.X, 1), 180.0 / Math.Max(Size.Y, 1));
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } up:
                ZoomAround(up.Position, 1 / ZoomStep);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } down:
                ZoomAround(down.Position, ZoomStep);
                break;
            case InputEventMouseButton
            {
                ButtonIndex: MouseButton.Right or MouseButton.Middle,
            } pan:
                _panning = pan.Pressed;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click:
                GrabFocus();
                Click(click.Position);
                break;
            case InputEventMouseMotion motion:
                _mouse = motion.Position;
                if (_panning)
                {
                    _centerLongitude -= motion.Relative.X * _degreesPerPixel;
                    _centerLatitude = Math.Clamp(
                        _centerLatitude + motion.Relative.Y * _degreesPerPixel, -90, 90);
                }

                QueueRedraw();
                break;
            default:
                return;
        }

        AcceptEvent();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), _background);
        DrawGrid();
        foreach (Constellation constellation in Constellations)
        {
            bool chosen = constellation.Id == ChosenId;
            foreach (StarLink line in constellation.Lines)
            {
                DrawArc(line, chosen ? _chosenColor : _lineColor, chosen ? 2.0f : 1.2f);
            }
        }

        foreach (StarField.Star star in _stars)
        {
            if (ScreenOf(star.Direction) is Vector2 at)
            {
                float radius = 0.7f + 2.6f * (float)star.Brightness;
                DrawCircle(at, radius, StarField.ColorOf(star.Temperature).ToGodot());
            }
        }

        DrawNames();
        DrawPending();
    }

    private void Click(Vector2 position)
    {
        if (StarNear(position) is int star)
        {
            StarClicked?.Invoke(star);
        }
        else if (ChosenLineNear(position) is StarLink line)
        {
            LineClicked?.Invoke(line);
        }
        else
        {
            EmptyClicked?.Invoke();
        }
    }

    private int? StarNear(Vector2 position)
    {
        int? best = null;
        float bestDistance = StarPickPixels;
        foreach (StarField.Star star in _stars)
        {
            if (ScreenOf(star.Direction) is Vector2 at && at.DistanceTo(position) < bestDistance)
            {
                best = star.Id;
                bestDistance = at.DistanceTo(position);
            }
        }

        return best;
    }

    private StarLink? ChosenLineNear(Vector2 position)
    {
        Constellation? chosen = Constellations.FirstOrDefault(c => c.Id == ChosenId);
        foreach (StarLink line in chosen?.Lines ?? [])
        {
            List<Vector2?> points = ArcPoints(line);
            for (int i = 1; i < points.Count; i++)
            {
                if (points[i - 1] is Vector2 a && points[i] is Vector2 b
                    && Geometry2D.GetClosestPointToSegment(position, a, b).DistanceTo(position)
                        < LinePickPixels)
                {
                    return line;
                }
            }
        }

        return null;
    }

    private void DrawGrid()
    {
        for (int latitude = -60; latitude <= 60; latitude += 30)
        {
            float y = (float)((_centerLatitude - latitude) / _degreesPerPixel + Size.Y / 2);
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), _gridColor);
        }

        for (int longitude = -180; longitude < 180; longitude += 30)
        {
            double offset = SphericalCoordinates.WrapLongitude(longitude - _centerLongitude);
            float x = (float)(offset / _degreesPerPixel + Size.X / 2);
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), _gridColor);
        }
    }

    private void DrawArc(StarLink line, Color color, float width)
    {
        List<Vector2?> points = ArcPoints(line);
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i - 1] is Vector2 a && points[i] is Vector2 b)
            {
                DrawLine(a, b, color, width, antialiased: true);
            }
        }
    }

    // Points along the line's great circle on screen; null where it wraps round the edge.
    private List<Vector2?> ArcPoints(StarLink line)
    {
        if (!_byId.TryGetValue(line.From, out StarField.Star from)
            || !_byId.TryGetValue(line.To, out StarField.Star to))
        {
            return [];
        }

        var points = new List<Vector2?>(ArcSegments + 1);
        Vector2? previous = null;
        for (int i = 0; i <= ArcSegments; i++)
        {
            Vector3D between = from.Direction * (1 - (double)i / ArcSegments)
                + to.Direction * ((double)i / ArcSegments);
            Vector2 at = MapPositionOf(between * (1 / between.Length));
            bool wrapped = previous is Vector2 last && Math.Abs(at.X - last.X) > Size.X / 2;
            points.Add(wrapped ? null : at);
            previous = at;
        }

        return points;
    }

    private void DrawNames()
    {
        Font font = GetThemeDefaultFont();
        foreach (Constellation constellation in Constellations)
        {
            List<Vector3D> stars = [.. constellation.Lines
                .SelectMany(line => new[] { line.From, line.To })
                .Distinct()
                .Where(_byId.ContainsKey)
                .Select(id => _byId[id].Direction)];
            if (stars.Count == 0)
            {
                continue;
            }

            Vector3D sum = stars.Aggregate((a, b) => a + b);
            if (sum.Length > 1e-6 && ScreenOf(sum * (1 / sum.Length)) is Vector2 at)
            {
                Color color = constellation.Id == ChosenId ? _chosenColor : _nameColor;
                DrawString(font, at + new Vector2(6, -8), constellation.Name,
                    modulate: color);
            }
        }
    }

    private void DrawPending()
    {
        if (PendingStar is not int id || !_byId.TryGetValue(id, out StarField.Star star)
            || ScreenOf(star.Direction) is not Vector2 at)
        {
            return;
        }

        DrawArc(at, 7, 0, Mathf.Tau, 24, _chosenColor, 1.5f, antialiased: true);
        DrawDashedLine(at, _mouse, _chosenColor, 1.0f, 5.0f);
    }

    // Where a direction is on screen, or null if it's off the canvas.
    private Vector2? ScreenOf(Vector3D direction)
    {
        Vector2 at = MapPositionOf(direction);
        return at.X < -10 || at.Y < -10 || at.X > Size.X + 10 || at.Y > Size.Y + 10
            ? null
            : at;
    }

    // Where a direction is on the flat map, its longitude taken nearest the view's middle.
    private Vector2 MapPositionOf(Vector3D direction)
    {
        double latitude = double.RadiansToDegrees(Math.Asin(Math.Clamp(direction.Y, -1, 1)));
        double longitude = double.RadiansToDegrees(Math.Atan2(direction.X, direction.Z));
        double across = SphericalCoordinates.WrapLongitude(longitude - _centerLongitude);
        return new Vector2((float)(across / _degreesPerPixel + Size.X / 2),
            (float)((_centerLatitude - latitude) / _degreesPerPixel + Size.Y / 2));
    }

    private void ZoomAround(Vector2 anchor, double factor)
    {
        double fitted = Math.Max(360.0 / Math.Max(Size.X, 1), 180.0 / Math.Max(Size.Y, 1));
        double zoomed = Math.Clamp(_degreesPerPixel * factor, MinDegreesPerPixel, fitted);
        Vector2 fromMiddle = anchor - Size / 2;

        // Keep the sky under the mouse where it is.
        _centerLongitude += fromMiddle.X * (_degreesPerPixel - zoomed);
        _centerLatitude = Math.Clamp(
            _centerLatitude - fromMiddle.Y * (_degreesPerPixel - zoomed), -90, 90);
        _degreesPerPixel = zoomed;
        QueueRedraw();
    }
}
