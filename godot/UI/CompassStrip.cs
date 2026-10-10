using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// A compass along the top of the first-person view (VISION.md REN-06): the points of the
/// compass and a tick every 5 degrees, sliding past as the view turns, with the heading under
/// the mark in the middle, and pinned places (journal entries and events) as marks under the
/// strip, labeled on a row of their own with their name and distance while there's room,
/// nearest first. Places out of view wait at the strip's ends, pointing the way to turn.
/// </summary>
public partial class CompassStrip : Control
{
    // Degrees of heading across the whole strip.
    private const float SpanDegrees = 120;

    private static readonly string[] _points = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];

    // The baselines of the heading's row and the places' labels' row, under it.
    private const float HeadingRow = 40;
    private const float LabelRow = 64;

    private readonly List<(float Bearing, Color Color, string Label)> _pins = [];
    private float _heading;

    /// <summary>Makes the strip, which ignores the mouse.</summary>
    public CompassStrip()
    {
        CustomMinimumSize = new Vector2(560, 70);
        Size = CustomMinimumSize;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>The way the view faces, in degrees clockwise from north.</summary>
    public float HeadingDegrees
    {
        get => _heading;
        set
        {
            float heading = (value % 360 + 360) % 360;
            if (Mathf.Abs(heading - _heading) > 0.01f)
            {
                _heading = heading;
                QueueRedraw();
            }
        }
    }

    /// <summary>
    /// The places to mark, nearest first: each one's bearing in degrees clockwise from north,
    /// color, and label.
    /// </summary>
    public void SetPins(IEnumerable<(double BearingDegrees, Color Color, string Label)> pins)
    {
        var next = pins.Select(pin => ((float)pin.BearingDegrees, pin.Color, pin.Label)).ToList();
        if (!next.SequenceEqual(_pins))
        {
            _pins.Clear();
            _pins.AddRange(next);
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        Font font = GetThemeDefaultFont();
        int fontSize = GetThemeDefaultFontSize();
        float width = Size.X, middle = width / 2, pixelsPerDegree = width / SpanDegrees;
        DrawRect(new Rect2(0, 0, width, 24), new Color(0, 0, 0, 0.35f));

        int first = (int)Mathf.Ceil((_heading - SpanDegrees / 2) / 5) * 5;
        for (int degrees = first; degrees <= _heading + SpanDegrees / 2; degrees += 5)
        {
            float x = middle + (degrees - _heading) * pixelsPerDegree;
            int around = (degrees % 360 + 360) % 360;
            bool point = around % 45 == 0;
            float tick = point ? 10 : around % 15 == 0 ? 6 : 3;
            DrawLine(new Vector2(x, 0), new Vector2(x, tick), new Color(1, 1, 1, 0.8f), 1);
            if (point)
            {
                string label = _points[around / 45];
                Color color = around == 0 ? new Color(1, 0.55f, 0.45f) : Colors.White;
                DrawText(font, fontSize, label, x, 22, color);
            }
        }

        // The mark in the middle (short, so a point's letter under it still reads) and the
        // heading under the strip.
        DrawLine(new Vector2(middle, 0), new Vector2(middle, 9), new Color(1, 0.85f, 0.3f), 2);
        string heading = $"{Mathf.RoundToInt(_heading) % 360}°";
        DrawText(font, fontSize - 2, heading, middle, HeadingRow, new Color(1, 0.85f, 0.3f));
        DrawPins(font, fontSize - 2);
    }

    // Each place as a small mark under the strip at its bearing (or at an end, pointing the
    // way to turn), with its label under it unless that would cover one already written.
    private void DrawPins(Font font, int size)
    {
        const float gap = 8;
        var taken = new List<(float Left, float Right)>();
        float width = Size.X, middle = width / 2, pixelsPerDegree = width / SpanDegrees;
        foreach ((float bearing, Color color, string name) in _pins)
        {
            string label = name;
            float offset = Mathf.Wrap(bearing - _heading, -180, 180);
            float x;
            if (Mathf.Abs(offset) <= SpanDegrees / 2)
            {
                x = middle + offset * pixelsPerDegree;
                DrawMark([new(x, 44), new(x - 5, 51), new(x + 5, 51)], color);
            }
            else
            {
                float side = Mathf.Sign(offset);
                x = side < 0 ? 0 : width;
                label = side < 0 ? $"‹ {name}" : $"{name} ›";
                DrawMark([new(x, 12), new(x - side * 8, 6), new(x - side * 8, 18)], color);
            }

            float labelWidth = font.GetStringSize(label, HorizontalAlignment.Left, -1, size).X;
            float left = Mathf.Clamp(x - labelWidth / 2, 0, width - labelWidth);
            float right = left + labelWidth;
            if (!taken.Any(span => left < span.Right + gap && right > span.Left - gap))
            {
                taken.Add((left, right));
                DrawText(font, size, label, left + labelWidth / 2, LabelRow, Colors.White);
            }
        }
    }

    // A small filled triangle in a place's color, outlined so it shows against any sky.
    private void DrawMark(Vector2[] corners, Color color)
    {
        DrawColoredPolygon(corners, color);
        DrawPolyline([.. corners, corners[0]], Colors.Black, 1);
    }

    // Text centered on x, with its baseline at y, outlined so it reads against any sky.
    private void DrawText(Font font, int size, string text, float x, float y, Color color)
    {
        float width = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        var at = new Vector2(x - width / 2, y);
        DrawStringOutline(font, at, text, HorizontalAlignment.Left, -1, size, 3, Colors.Black);
        DrawString(font, at, text, HorizontalAlignment.Left, -1, size, color);
    }
}
