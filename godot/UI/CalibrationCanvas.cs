using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;

namespace NothicWorlds.UI;

/// <summary>
/// Shows the flat map image with its calibration guide lines (VISION.md MAP-05), and lets the
/// user drag them. Each line is drawn where the calibration currently places that true
/// latitude/longitude, so it can be a curve (Robinson meridians) or a ring or spoke (Polar).
/// Drag a line to move it; right-click a line to remove it.
/// </summary>
public partial class CalibrationCanvas : Control
{
    private const float ImageMargin = 8.0f;
    private const float GrabDistance = 8.0f;
    private const float LineWidth = 2.0f;
    private const float HighlightWidth = 3.5f;
    private const double LineStepDegrees = 1.0;

    // Consecutive points further apart than this (a fraction of the image width) belong to
    // different pieces of a line, e.g. across the gap between two hemisphere circles.
    private const float MaxJumpFraction = 0.2f;

    private static readonly Color _latitudeColor = new(0.35f, 0.85f, 1.0f);
    private static readonly Color _longitudeColor = new(1.0f, 0.7f, 0.3f);
    private static readonly Color _highlightColor = Colors.White;

    private readonly List<GuideLine> _lines = [];
    private Texture2D? _texture;
    private MapProjection _projection;
    private double _aspectRatio = 2.0;
    private MapCalibration _calibration = MapCalibration.CreateDefault();
    private MapProjectionInverter? _inverter;
    private GuideId? _hovered;
    private GuideId? _dragging;

    /// <summary>
    /// Raised whenever the user changes the calibration (while dragging, or on removal).
    /// </summary>
    public event Action<MapCalibration>? CalibrationChanged;

    /// <summary>The calibration currently shown.</summary>
    public MapCalibration Calibration => _calibration;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Resized += Rebuild;
    }

    /// <summary>Shows a map image and its calibration, ready for editing.</summary>
    public void Load(
        Texture2D texture, MapProjection projection, double aspectRatio, MapCalibration calibration)
    {
        _texture = texture;
        _projection = projection;
        _aspectRatio = aspectRatio;
        _inverter = new MapProjectionInverter(projection, aspectRatio);
        _hovered = null;
        _dragging = null;
        SetCalibration(calibration);
    }

    /// <summary>Replaces the calibration shown (e.g. after Reset or adding a guide).</summary>
    public void SetCalibration(MapCalibration calibration)
    {
        _calibration = calibration;
        Rebuild();
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion when _dragging is GuideId guide:
                DragTo(guide, motion.Position);
                break;
            case InputEventMouseMotion motion:
                SetHovered(FindLineNear(motion.Position));
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed ? _hovered : null;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }
                when _hovered is GuideId guide:
                Remove(guide);
                break;
            default:
                return;
        }

        AcceptEvent();
    }

    public override void _Draw()
    {
        if (_texture is null)
        {
            return;
        }

        DrawTextureRect(_texture, ImageRect(), tile: false);
        Font font = GetThemeDefaultFont();
        foreach (GuideLine line in _lines)
        {
            bool highlighted = line.Id == _hovered || line.Id == _dragging;
            Color color = highlighted
                ? _highlightColor
                : line.Id.IsLatitude ? _latitudeColor : _longitudeColor;
            float width = highlighted ? HighlightWidth : LineWidth;
            foreach (Vector2[] piece in line.Pieces.Where(p => p.Length >= 2))
            {
                DrawPolyline(piece, Colors.Black, width + 2.0f, antialiased: true);  // Outline
                DrawPolyline(piece, color, width, antialiased: true);
            }

            if (line.LabelAt is Vector2 anchor)
            {
                Vector2 at = anchor + new Vector2(4, -4);
                DrawString(font, at + Vector2.One, line.Label, modulate: Colors.Black);
                DrawString(font, at, line.Label, modulate: color);
            }
        }
    }

    // Moves a guide so it passes under the mouse: the map type's latitude/longitude there is
    // exactly the new "drawn as" value.
    private void DragTo(GuideId guide, Vector2 mouse)
    {
        Rect2 rect = ImageRect();
        Vector2 uv = (mouse - rect.Position) / rect.Size;
        if (_inverter?.Invert(uv.X, uv.Y) is not GeoCoordinate drawn)
        {
            return;  // Off the map; keep the line where it was.
        }

        MapCalibration updated = guide.IsLatitude
            ? _calibration.WithLatitudeDrawnAs(guide.Index, drawn.LatitudeDegrees)
            : _calibration.WithLongitudeDrawnAs(guide.Index, drawn.LongitudeDegrees);
        SetCalibration(updated);
        CalibrationChanged?.Invoke(updated);
    }

    private void Remove(GuideId guide)
    {
        MapCalibration updated = guide.IsLatitude
            ? _calibration.RemoveLatitude(guide.Index)
            : _calibration.RemoveLongitude(guide.Index);
        _hovered = null;
        SetCalibration(updated);
        CalibrationChanged?.Invoke(updated);
    }

    private void SetHovered(GuideId? guide)
    {
        if (guide == _hovered)
        {
            return;
        }

        _hovered = guide;
        MouseDefaultCursorShape = guide is null ? CursorShape.Arrow : CursorShape.Drag;
        TooltipText = guide is null ? "" : "Drag to move this line. Right-click to remove it.";
        QueueRedraw();
    }

    private GuideId? FindLineNear(Vector2 point)
    {
        GuideId? nearest = null;
        float best = GrabDistance;
        foreach (GuideLine line in _lines)
        {
            foreach (Vector2[] piece in line.Pieces)
            {
                for (int i = 0; i + 1 < piece.Length; i++)
                {
                    float distance = DistanceToSegment(point, piece[i], piece[i + 1]);
                    if (distance < best)
                    {
                        best = distance;
                        nearest = line.Id;
                    }
                }
            }
        }

        return nearest;
    }

    // Recomputes where every guide line is drawn on screen.
    private void Rebuild()
    {
        _lines.Clear();
        if (_texture is null)
        {
            QueueRedraw();
            return;
        }

        // Labels go where lines are well spread out: latitude labels at 150°W (clear of the 180°
        // line and of polar spokes), longitude labels at 45°S (where meridians haven't yet
        // bunched up toward a pole).
        for (int i = 0; i < _calibration.Latitudes.Count; i++)
        {
            CalibrationGuide guide = _calibration.Latitudes[i];
            IEnumerable<GeoCoordinate> path = Steps(-180.0, 180.0).Select(lon =>
                new GeoCoordinate(guide.DrawnAsDegrees, _calibration.DrawnLongitude(lon)));
            var labelSpot = new GeoCoordinate(
                guide.DrawnAsDegrees, _calibration.DrawnLongitude(-150.0));
            AddLine(new GuideId(true, i), LatitudeLabel(guide.Degrees), path, labelSpot);
        }

        for (int i = 0; i < _calibration.Longitudes.Count; i++)
        {
            CalibrationGuide guide = _calibration.Longitudes[i];
            IEnumerable<GeoCoordinate> path = Steps(-90.0, 90.0).Select(lat =>
                new GeoCoordinate(_calibration.DrawnLatitude(lat), guide.DrawnAsDegrees));
            var labelSpot = new GeoCoordinate(
                _calibration.DrawnLatitude(-45.0), guide.DrawnAsDegrees);
            AddLine(new GuideId(false, i), LongitudeLabel(guide.Degrees), path, labelSpot);
        }

        QueueRedraw();
    }

    // Adds a line, labelled at `labelSpot` if that's on the map (otherwise at the line's start).
    private void AddLine(
        GuideId id, string label, IEnumerable<GeoCoordinate> path, GeoCoordinate labelSpot)
    {
        List<Vector2[]> pieces = ToScreenPieces(path);
        Vector2? labelAt = ToScreen(labelSpot)
            ?? pieces.FirstOrDefault(p => p.Length > 0)?[0];
        _lines.Add(new GuideLine(id, label, pieces, labelAt));
    }

    private Vector2? ToScreen(GeoCoordinate coordinate)
    {
        MapImagePosition p = MapProjections.ToImagePosition(coordinate, _projection, _aspectRatio);
        Rect2 rect = ImageRect();
        return p.IsOutsideMap
            ? null
            : rect.Position + new Vector2((float)p.U, (float)p.V) * rect.Size;
    }

    // Converts a path of drawn coordinates into screen polylines, breaking it where it leaves
    // the map or jumps (e.g. between hemisphere circles).
    private List<Vector2[]> ToScreenPieces(IEnumerable<GeoCoordinate> path)
    {
        Rect2 rect = ImageRect();
        float maxJump = rect.Size.X * MaxJumpFraction;
        var pieces = new List<Vector2[]>();
        var current = new List<Vector2>();
        foreach (GeoCoordinate coordinate in path)
        {
            MapImagePosition p =
                MapProjections.ToImagePosition(coordinate, _projection, _aspectRatio);
            var point = rect.Position + new Vector2((float)p.U, (float)p.V) * rect.Size;
            if (p.IsOutsideMap || (current.Count > 0 && current[^1].DistanceTo(point) > maxJump))
            {
                FinishPiece(pieces, current);
            }

            if (!p.IsOutsideMap)
            {
                current.Add(point);
            }
        }

        FinishPiece(pieces, current);
        return pieces;
    }

    // Where the image is drawn: as large as fits, keeping its shape, centered.
    private Rect2 ImageRect()
    {
        Vector2 space = Size - new Vector2(ImageMargin * 2, ImageMargin * 2);
        if (_texture is null || space.X <= 0 || space.Y <= 0)
        {
            return new Rect2(Vector2.Zero, Vector2.One);
        }

        float imageAspect = (float)_texture.GetWidth() / _texture.GetHeight();
        Vector2 size = space.X / space.Y > imageAspect
            ? new Vector2(space.Y * imageAspect, space.Y)
            : new Vector2(space.X, space.X / imageAspect);
        return new Rect2(ImageMargin * Vector2.One + (space - size) / 2, size);
    }

    private static void FinishPiece(List<Vector2[]> pieces, List<Vector2> current)
    {
        if (current.Count > 0)
        {
            pieces.Add([.. current]);
            current.Clear();
        }
    }

    private static IEnumerable<double> Steps(double from, double to)
    {
        for (double value = from; value <= to; value += LineStepDegrees)
        {
            yield return value;
        }
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.LengthSquared();
        float t = lengthSquared > 0
            ? Mathf.Clamp((point - a).Dot(ab) / lengthSquared, 0, 1)
            : 0;
        return point.DistanceTo(a + ab * t);
    }

    private static string LatitudeLabel(double degrees) =>
        degrees == 0 ? "0°" : $"{Math.Abs(degrees):0.#}°{(degrees > 0 ? "N" : "S")}";

    private static string LongitudeLabel(double degrees) =>
        degrees is 0 or -180 ? $"{Math.Abs(degrees):0}°"
            : $"{Math.Abs(degrees):0.#}°{(degrees > 0 ? "E" : "W")}";

    private readonly record struct GuideId(bool IsLatitude, int Index);

    private sealed record GuideLine(
        GuideId Id, string Label, List<Vector2[]> Pieces, Vector2? LabelAt);
}
