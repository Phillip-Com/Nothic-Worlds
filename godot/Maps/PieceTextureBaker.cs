using Godot;
using NothicWorlds.Core.Maps;

namespace NothicWorlds.Maps;

/// <summary>
/// Turns a cut into a texture the planet shader can draw (VISION.md MAP-02). It crops the source
/// image to the cut's bounding box, shrinks it if it's very large, applies the cut-out shape as
/// transparency (with smooth edges), builds mipmaps, and compresses it. Slow for big cuts, so call
/// it on a background thread.
/// </summary>
public static class PieceTextureBaker
{
    /// <summary>The longest side a piece texture keeps; larger cuts are shrunk to this.</summary>
    public const int MaxSize = 4096;

    /// <summary>Bakes the piece texture. The caller disposes the result.</summary>
    /// <param name="source">The full-resolution source image (not modified).</param>
    /// <param name="outline">The cut-out shape on that image.</param>
    public static Image Bake(Image source, PieceOutline outline)
    {
        int sourceWidth = source.GetWidth();
        int sourceHeight = source.GetHeight();
        int left = Math.Clamp((int)Math.Floor(outline.MinU * sourceWidth), 0, sourceWidth - 1);
        int top = Math.Clamp((int)Math.Floor(outline.MinV * sourceHeight), 0, sourceHeight - 1);
        int right =
            Math.Clamp((int)Math.Ceiling(outline.MaxU * sourceWidth), left + 1, sourceWidth);
        int bottom =
            Math.Clamp((int)Math.Ceiling(outline.MaxV * sourceHeight), top + 1, sourceHeight);

        Image piece = source.GetRegion(new Rect2I(left, top, right - left, bottom - top));
        if (piece.IsCompressed())
        {
            piece.Decompress();
        }

        piece.Convert(Image.Format.Rgba8);
        int longest = Math.Max(piece.GetWidth(), piece.GetHeight());
        if (longest > MaxSize)
        {
            double scale = (double)MaxSize / longest;
            MapImageLoader.ShrinkTo(piece,
                Math.Max(1, (int)Math.Round(piece.GetWidth() * scale)),
                Math.Max(1, (int)Math.Round(piece.GetHeight() * scale)));
        }

        ApplyMask(piece, outline);
        piece.GenerateMipmaps();

        // With transparency this becomes DXT5: about a quarter of the memory of raw pixels.
        Error compressed = piece.Compress(Image.CompressMode.S3Tc, Image.CompressSource.Srgb);
        if (compressed != Error.Ok)
        {
            GD.PushWarning($"Piece compression failed ({compressed}); using it uncompressed.");
        }

        return piece;
    }

    // Makes everything outside the cut transparent. Colors are left as they are, so smooth
    // edges blend toward the image's own colors rather than a dark fringe.
    private static void ApplyMask(Image piece, PieceOutline outline)
    {
        int width = piece.GetWidth();
        int height = piece.GetHeight();
        byte[] mask = outline.RasterizeMask(width, height);
        byte[] pixels = piece.GetData();
        for (int i = 0; i < mask.Length; i++)
        {
            int alpha = i * 4 + 3;
            pixels[alpha] = Math.Min(pixels[alpha], mask[i]);
        }

        piece.SetData(width, height, false, Image.Format.Rgba8, pixels);
    }
}
