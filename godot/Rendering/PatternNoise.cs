using Godot;

namespace NothicWorlds.Rendering;

/// <summary>
/// A small repeating 3D texture of random values (32 × 32 × 32, 32 KB), shared by every globe,
/// that <c>planet.gdshader</c> reads with smooth filtering to draw surface patterns (VISION.md
/// BOD-06). Reading noise from a texture is several times cheaper on integrated graphics than
/// working it out in every pixel. Built once, from a fixed seed, so patterns never change.
/// </summary>
public static class PatternNoise
{
    // Must match PATTERN_NOISE_SIZE in planet.gdshader.
    private const int Size = 32;

    private static ImageTexture3D? _texture;

    /// <summary>The noise texture (built on first use).</summary>
    public static ImageTexture3D Texture => _texture ??= Create();

    private static ImageTexture3D Create()
    {
        var random = new Random(20261003);  // Fixed: the same patterns every time
        var layers = new Godot.Collections.Array<Image>();
        for (int z = 0; z < Size; z++)
        {
            var values = new byte[Size * Size];
            random.NextBytes(values);
            layers.Add(Image.CreateFromData(Size, Size, false, Image.Format.R8, values));
        }

        var texture = new ImageTexture3D();
        texture.Create(Image.Format.R8, Size, Size, Size, false, layers);
        return texture;
    }
}
