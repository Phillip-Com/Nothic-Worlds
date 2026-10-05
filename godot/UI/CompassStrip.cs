using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// A compass along the top of the first-person view (VISION.md REN-06): the points of the
/// compass and a tick every 5 degrees, sliding past as the view turns, with the heading under
/// the mark in the middle.
/// </summary>
public partial class CompassStrip : Control
{
    // Degrees of heading across the whole strip.
    private const float SpanDegrees = 120;

    private static readonly string[] _points = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];

    private float _heading;

    /// <summary>Makes the strip, which ignores the mouse.</summary>
    public CompassStrip()
    {
        CustomMinimumSize = new Vector2(560, 40);
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
        DrawText(font, fontSize - 2, $"{Mathf.RoundToInt(_heading) % 360}°", middle, 38,
            new Color(1, 0.85f, 0.3f));
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
