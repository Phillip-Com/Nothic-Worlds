using Godot;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.UI;

/// <summary>
/// A moon drawn as it looks in its phase (VISION.md CAL-05): a dark disc with its lit part,
/// lit on the right while waxing and on the left while waning (as seen from the north). The
/// tooltip names the moon and its phase.
/// </summary>
public partial class MoonIcon : Control
{
    private const int Points = 24;
    private static readonly Color _dark = new(0.16f, 0.17f, 0.21f);
    private static readonly Color _lit = new(0.93f, 0.92f, 0.84f);
    private static readonly Color _rim = new(0.45f, 0.47f, 0.53f);
    private MoonPhase _phase;

    /// <summary>Makes an icon <paramref name="size"/> pixels across.</summary>
    public MoonIcon(float size)
    {
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Pass;
    }

    /// <summary>The phase shown.</summary>
    public MoonPhase Phase
    {
        get => _phase;
        set
        {
            _phase = value;
            TooltipText = $"{value.Name}: {value.PhaseName} ({value.Lit:P0} lit)";
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        float radius = Math.Min(Size.X, Size.Y) / 2 - 0.5f;
        Vector2 middle = Size / 2;
        DrawCircle(middle, radius, _dark);

        // The lit part: between the limb on the lit side and the terminator, an ellipse whose
        // width runs from the limb (new) through a straight line (half) to the far limb
        // (full). Drawn as thin strips from bottom to top, which never fail to fill, even for
        // the slimmest crescent.
        float side = _phase.Waxing ? 1 : -1;
        float squash = (float)(1 - 2 * _phase.Lit);
        if (_phase.Lit > 0.005)
        {
            Vector2? lastLimb = null, lastEdge = null;
            for (int i = 0; i < Points; i++)
            {
                float angle = Mathf.Pi * (i / (Points - 1f) - 0.5f);  // Bottom to top
                var limb = middle
                    + new Vector2(side * Mathf.Cos(angle), -Mathf.Sin(angle)) * radius;
                var edge = middle
                    + new Vector2(side * squash * Mathf.Cos(angle), -Mathf.Sin(angle)) * radius;
                if (lastLimb is Vector2 l && lastEdge is Vector2 e)
                {
                    DrawPrimitive([l, limb, edge, e], [_lit, _lit, _lit, _lit], []);
                }

                (lastLimb, lastEdge) = (limb, edge);
            }
        }

        DrawArc(middle, radius, 0, Mathf.Tau, 32, _rim, 1, antialiased: true);
    }
}
