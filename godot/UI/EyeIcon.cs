using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// A small eye icon for show/hide buttons in lists (as in layer lists), drawn in code so no
/// image file is needed: open when shown, dimmed and struck through when hidden.
/// </summary>
public static class EyeIcon
{
    private const int Size = 16;
    private static ImageTexture? _open, _closed;

    /// <summary>The open eye (shown) or the struck-through one (hidden).</summary>
    public static ImageTexture Get(bool open) => open
        ? _open ??= ImageTexture.CreateFromImage(Draw(struck: false))
        : _closed ??= ImageTexture.CreateFromImage(Draw(struck: true));

    private static Image Draw(bool struck)
    {
        // The eye's outline is where two big circles overlap (an almond shape).
        const float center = Size / 2f, halfWidth = 7f, halfHeight = 4.5f;
        const float radius = (halfWidth * halfWidth + halfHeight * halfHeight) / (2 * halfHeight);
        const float circleOffset = radius - halfHeight;
        float strength = struck ? 0.5f : 0.9f;

        Image image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);
                float edge = Mathf.Max(
                    point.DistanceTo(new Vector2(center, center - circleOffset)) - radius,
                    point.DistanceTo(new Vector2(center, center + circleOffset)) - radius);
                float outline = Coverage(Mathf.Abs(edge), 0.6f);
                float pupil = Coverage(point.DistanceTo(new Vector2(center, center)), 2.2f);
                float alpha = Mathf.Max(outline, pupil) * strength;
                if (struck)
                {
                    // A line from bottom left to top right, over the eye.
                    float fromLine = Mathf.Abs(point.X + point.Y - Size) / Mathf.Sqrt2;
                    alpha = Mathf.Max(alpha, Coverage(fromLine, 0.8f) * 0.9f);
                }

                image.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }

        return image;
    }

    // How much of a pixel this far from a shape's middle is covered by a shape this wide
    // (half-width), softened over about a pixel so its edges are smooth.
    private static float Coverage(float distance, float halfWidth) =>
        Mathf.Clamp(halfWidth + 0.5f - distance, 0, 1);
}
