using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// The round minimap in the first-person view (VISION.md REN-08; owner's choice): what a camera
/// above the spot sees, north up, with the roads over it (BOD-13) and an arrow for where the
/// eye stands and which way it faces. The wheel over it or its + and − buttons zoom; a click
/// on it asks to travel there. It only shows what it's given;
/// <see cref="Controls.FirstPersonMode"/> places the camera.
/// </summary>
public partial class Minimap : Control
{
    /// <summary>How wide the map is on screen, in pixels.</summary>
    public const int Diameter = 220;

    private const string MaskShader = """
        shader_type canvas_item;
        // Only the round map shows, its edge smoothed over a pixel.
        void fragment() {
            float from_middle = length(UV - 0.5) * 2.0;
            float edge = fwidth(from_middle);
            COLOR = texture(TEXTURE, UV);
            COLOR.a *= 1.0 - smoothstep(1.0 - edge, 1.0, from_middle);
        }
        """;

    // How long each dash of a dashed line is, and the gap after it, in pixels.
    private const float DashPixels = 5;

    private static readonly Color _ring = new(0.75f, 0.78f, 0.86f, 0.9f);
    private static readonly Color _arrow = new(1.0f, 0.82f, 0.30f);
    private readonly TextureRect _picture;
    private readonly Control _marks;  // Over the picture: the ring, north, and the arrow
    private readonly Label _scale;
    private float _heading;
    private IReadOnlyList<MinimapLine> _lines = [];

    /// <summary>Makes the minimap; give it a picture with <see cref="SetPicture"/>.</summary>
    public Minimap()
    {
        CustomMinimumSize = new Vector2(Diameter, Diameter + 34);
        Size = CustomMinimumSize;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = "Where you are, north up. Click a spot to go there; the wheel zooms";
        _picture = new TextureRect
        {
            Size = new Vector2(Diameter, Diameter),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = new Shader { Code = MaskShader } },
        };
        AddChild(_picture);
        _marks = new Control
        {
            Size = new Vector2(Diameter, Diameter),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _marks.Draw += DrawMarks;
        AddChild(_marks);

        var row = new HBoxContainer
        {
            Position = new Vector2(0, Diameter + 4),
            Size = new Vector2(Diameter, 28),
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        row.AddChild(ZoomButton("−", +1, "See farther (zoom out)"));
        _scale = new Label
        {
            CustomMinimumSize = new Vector2(110, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Pass,
            TooltipText = "How far it is across the map",
        };
        _scale.AddThemeColorOverride("font_outline_color", Colors.Black);
        _scale.AddThemeConstantOverride("outline_size", 4);
        row.AddChild(_scale);
        row.AddChild(ZoomButton("+", -1, "See closer (zoom in)"));
        AddChild(row);
    }

    /// <summary>
    /// Called with the place clicked, from -1 to 1 across the map (x east, y north; 0 is the
    /// middle, where the eye is).
    /// </summary>
    public event Action<Vector2>? Clicked;

    /// <summary>Called with +1 to zoom out a step, -1 to zoom in.</summary>
    public event Action<int>? Zoomed;

    /// <summary>The picture of the ground below (the overhead camera's).</summary>
    public void SetPicture(Texture2D texture) => _picture.Texture = texture;

    /// <summary>
    /// The way the eye faces, in degrees clockwise from north, and how far the map reaches
    /// across, as words ("40 km across").
    /// </summary>
    public void Show(double headingDegrees, string across)
    {
        _heading = (float)headingDegrees;
        _scale.Text = across;
        _marks.QueueRedraw();
    }

    /// <summary>The lines drawn over the map (the roads), replacing those before.</summary>
    public void SetLines(IReadOnlyList<MinimapLine> lines)
    {
        _lines = lines;
        _marks.QueueRedraw();
    }

    private void DrawMarks()
    {
        var middle = new Vector2(Diameter, Diameter) / 2;
        float radius = Diameter / 2f;
        foreach (MinimapLine line in _lines)
        {
            DrawLine(line, middle, radius);
        }

        _marks.DrawArc(middle, radius - 1, 0, Mathf.Tau, 64, _ring, 2, antialiased: true);
        _marks.DrawString(ThemeDB.FallbackFont, middle + new Vector2(-5, -radius + 18), "N",
            HorizontalAlignment.Left, -1, 15, _ring);

        // The arrow, pointing the way the eye faces (north up, clockwise).
        float turn = Mathf.DegToRad(_heading);
        Vector2 Rotated(Vector2 point) => middle + point.Rotated(turn);
        Vector2[] arrow = [Rotated(new(0, -11)), Rotated(new(7, 8)), Rotated(new(0, 4)),
            Rotated(new(-7, 8))];
        _marks.DrawColoredPolygon(arrow, _arrow);
        _marks.DrawPolyline([.. arrow, arrow[0]], Colors.Black, 1.5f, antialiased: true);
    }

    // A line cut at the map's round edge, on a dark edge of its own so it reads over any
    // ground. Its map points (north up) become the control's (y down). Dashes run on along the
    // whole line, not each piece of it (a road's pieces can be shorter than a dash).
    private void DrawLine(MinimapLine line, Vector2 middle, float radius)
    {
        float along = 0;  // How far along the line, in pixels, for its dashes
        for (int i = 1; i < line.Points.Length; i++)
        {
            if (InsideCircle(line.Points[i - 1], line.Points[i]) is not var (from, to))
            {
                continue;
            }

            Vector2 a = middle + new Vector2(from.X, -from.Y) * radius;
            Vector2 b = middle + new Vector2(to.X, -to.Y) * radius;
            float length = a.DistanceTo(b);
            if (!line.Dashed || length < 1e-4f)
            {
                DrawStroke(a, b, line);
                along += length;
                continue;
            }

            Vector2 step = (b - a) / length;
            for (float start = 0; start < length;)
            {
                float phase = (along + start) % (2 * DashPixels);
                bool on = phase < DashPixels;
                float end = Math.Min(length, start + (on ? DashPixels : 2 * DashPixels) - phase);
                if (on)
                {
                    DrawStroke(a + step * start, a + step * end, line);
                }

                start = end;
            }

            along += length;
        }
    }

    private void DrawStroke(Vector2 from, Vector2 to, MinimapLine line)
    {
        _marks.DrawLine(from, to, Colors.Black with { A = 0.6f }, line.Width + 2,
            antialiased: true);
        _marks.DrawLine(from, to, line.Color, line.Width, antialiased: true);
    }

    // The part of the segment from a to b inside the unit circle, or null if none is.
    private static (Vector2 From, Vector2 To)? InsideCircle(Vector2 a, Vector2 b)
    {
        // Where |a + t (b − a)| = 1: a quadratic in t; the inside is between its roots.
        Vector2 d = b - a;
        float qa = d.Dot(d), qb = 2 * a.Dot(d), qc = a.Dot(a) - 1;
        if (qa < 1e-12f)
        {
            return qc <= 0 ? (a, b) : null;
        }

        float disc = qb * qb - 4 * qa * qc;
        if (disc <= 0)
        {
            return null;
        }

        float root = Mathf.Sqrt(disc);
        float enter = Math.Max(0, (-qb - root) / (2 * qa));
        float leave = Math.Min(1, (-qb + root) / (2 * qa));
        return enter < leave ? (a + d * enter, a + d * leave) : null;
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                Zoomed?.Invoke(-1);
                AcceptEvent();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                Zoomed?.Invoke(+1);
                AcceptEvent();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
                when click.Position.Y <= Diameter:
                Vector2 across = (click.Position - new Vector2(Diameter, Diameter) / 2)
                    / (Diameter / 2f);
                if (across.Length() <= 1)
                {
                    Clicked?.Invoke(new Vector2(across.X, -across.Y));
                }

                AcceptEvent();
                break;
        }
    }

    private Button ZoomButton(string text, int step, string tooltip)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(30, 0),
        };
        button.Pressed += () => Zoomed?.Invoke(step);
        return button;
    }
}
