using Godot;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Rendering;

/// <summary>
/// The images a globe's terrain and heights are drawn from (see <see cref="PlanetSurface"/>):
/// each cube face's terrain codes, its far-away colors, and its heights. Plain functions of
/// their inputs, so they can be made on worker threads: a whole globe's take a few tenths of a
/// second, which froze the app when it was done while opening a world (VISION.md REN-03).
/// </summary>
public static class SurfaceImages
{
    /// <summary>The far-away copy's size across a face, in texels.</summary>
    public const int FarSize = 256;

    // The cube's faces.
    private const int Faces = 6;

    // How many cells across each far texel averages.
    private const int FarBlock = TerrainGrid.FaceSize / FarSize;

    /// <summary>One face of a terrain grid as a one-byte-per-cell image.</summary>
    public static Image TerrainFace(TerrainGrid terrain, int face)
    {
        var cells = new byte[TerrainGrid.CellsPerFace];
        terrain.CopyFace(face, cells);
        return Image.CreateFromData(
            TerrainGrid.FaceSize, TerrainGrid.FaceSize, false, Image.Format.R8, cells);
    }

    /// <summary>
    /// One face of a terrain grid averaged over blocks of cells into colors, by
    /// <paramref name="palette"/> (RGBA per code; alpha for how painted a code draws), with
    /// mipmaps for ever farther views.
    /// </summary>
    public static Image FarFace(TerrainGrid terrain, int face, byte[] palette)
    {
        var cells = new byte[TerrainGrid.CellsPerFace];
        terrain.CopyFace(face, cells);
        return FarFace(cells, palette);
    }

    /// <summary>As <see cref="FarFace(TerrainGrid, int, byte[])"/>, from a face's cells.</summary>
    public static Image FarFace(byte[] cells, byte[] palette)
    {
        var colors = new byte[FarSize * FarSize * 4];
        bool hasPalette = palette.Length > 0;
        for (int farRow = 0; farRow < FarSize; farRow++)
        {
            for (int farColumn = 0; farColumn < FarSize; farColumn++)
            {
                int red = 0, green = 0, blue = 0, alpha = 0;
                for (int row = 0; row < FarBlock && hasPalette; row++)
                {
                    int start = (farRow * FarBlock + row) * TerrainGrid.FaceSize
                        + farColumn * FarBlock;
                    for (int column = 0; column < FarBlock; column++)
                    {
                        int at = cells[start + column] * 4;
                        int cellAlpha = palette[at + 3];
                        red += palette[at] * cellAlpha / 255;
                        green += palette[at + 1] * cellAlpha / 255;
                        blue += palette[at + 2] * cellAlpha / 255;
                        alpha += cellAlpha;
                    }
                }

                const int count = FarBlock * FarBlock;
                int texel = (farRow * FarSize + farColumn) * 4;
                colors[texel] = (byte)(red / count);
                colors[texel + 1] = (byte)(green / count);
                colors[texel + 2] = (byte)(blue / count);
                colors[texel + 3] = (byte)(alpha / count);
            }
        }

        Image image = Image.CreateFromData(FarSize, FarSize, false, Image.Format.Rgba8, colors);
        image.GenerateMipmaps();
        return image;
    }

    /// <summary>
    /// One face's heights as an image of half-precision floats in meters, as the shader
    /// reads them, with its smaller copies unless <paramref name="mipmaps"/> is false.
    /// </summary>
    public static Image HeightFace(HeightGrid heights, int face, bool mipmaps = true)
    {
        var values = new short[HeightGrid.CellsPerFace];
        heights.CopyFace(face, values);
        var bytes = new byte[values.Length * 2];
        for (int index = 0; index < values.Length; index++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(index * 2), (Half)values[index]);
        }

        Image image = Image.CreateFromData(HeightGrid.FaceSize, HeightGrid.FaceSize, false,
            Image.Format.Rh, bytes);
        if (mipmaps)
        {
            image.GenerateMipmaps();
        }

        return image;
    }

    /// <summary>
    /// The images for showing <paramref name="terrain"/> and <paramref name="heights"/> on a
    /// globe that shows <paramref name="terrainBefore"/> and <paramref name="heightsBefore"/>
    /// (null where it has no images yet: then every face is made). Only faces that differ are
    /// made, side by side; none for an empty grid (the globe just drops its images). Safe to
    /// call on a worker thread.
    /// </summary>
    public static PreparedSurface Prepare(TerrainGrid terrain, TerrainGrid? terrainBefore,
        byte[] palette, HeightGrid heights, HeightGrid? heightsBefore)
    {
        bool[] terrainFaces = Changed(terrain.IsEmpty, terrainBefore is null
            ? null
            : terrain.FacesChangedFrom(terrainBefore));
        bool[] heightFaces = Changed(heights.IsEmpty, heightsBefore is null
            ? null
            : heights.FacesChangedFrom(heightsBefore));
        Image?[]? codes = terrainFaces.Any(face => face) ? new Image?[Faces] : null;
        Image?[]? far = codes is null ? null : new Image?[Faces];
        Image?[]? lifts = heightFaces.Any(face => face) ? new Image?[Faces] : null;
        Parallel.For(0, Faces, face =>
        {
            if (codes is not null && far is not null && terrainFaces[face])
            {
                codes[face] = TerrainFace(terrain, face);
                far[face] = FarFace(terrain, face, palette);
            }

            if (lifts is not null && heightFaces[face])
            {
                lifts[face] = HeightFace(heights, face);
            }
        });
        return new PreparedSurface(terrain, terrainBefore, palette, codes, far, heights,
            heightsBefore, lifts);
    }

    // Which faces to make: none for an empty grid, all with nothing shown before, else those
    // that changed.
    private static bool[] Changed(bool empty, IEnumerable<int>? changed)
    {
        var make = new bool[Faces];
        if (empty)
        {
            return make;
        }

        foreach (int face in changed ?? Enumerable.Range(0, Faces))
        {
            make[face] = true;
        }

        return make;
    }
}
