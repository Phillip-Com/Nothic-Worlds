using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// A small clock face for the time of day on a body (VISION.md CAL-05): one hand going round
/// once a day, whatever the day's length, with noon at the top and midnight at the bottom;
/// the upper half is lighter, for day.
/// </summary>
public partial class ClockFace : Control
{
    private static readonly Color _night = new(0.10f, 0.11f, 0.16f);
    private static readonly Color _day = new(0.30f, 0.38f, 0.52f);
    private static readonly Color _rim = new(0.45f, 0.47f, 0.53f);
    private static readonly Color _hand = new(1.0f, 0.82f, 0.40f);
    private double _dayFraction;

    /// <summary>Makes a clock <paramref name="size"/> pixels across.</summary>
    public ClockFace(float size)
    {
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Pass;
    }

    /// <summary>How far through the day it is: 0 at midnight, 0.5 at noon.</summary>
    public double DayFraction
    {
        get => _dayFraction;
        set
        {
            _dayFraction = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        float radius = Math.Min(Size.X, Size.Y) / 2 - 0.5f;
        Vector2 middle = Size / 2;
        DrawCircle(middle, radius, _night);
        DrawArc(middle, radius / 2, Mathf.Pi, Mathf.Tau, 16, _day, radius, antialiased: true);
        DrawArc(middle, radius, 0, Mathf.Tau, 32, _rim, 1, antialiased: true);

        // Midnight at the bottom, turning clockwise through dawn on the left to noon at the top.
        float angle = Mathf.Pi / 2 + (float)_dayFraction * Mathf.Tau;
        var tip = middle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius - 2);
        DrawLine(middle, tip, _hand, 2, antialiased: true);
        DrawCircle(middle, 1.5f, _hand);
    }
}
