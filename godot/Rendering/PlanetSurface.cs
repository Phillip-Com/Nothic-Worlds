using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;
using NothicWorlds.Interop;

namespace NothicWorlds.Rendering;

/// <summary>
/// Controls what's drawn on one planet's or moon's surface through <c>planet.gdshader</c>: an
/// imported map (VISION.md MAP-01), how it wraps onto the globe (MAP-03, MAP-04), map pieces
/// (MAP-02), painted terrain (BOD-05), and the latitude/longitude grid. The grid shows by default
/// and hides when a map is applied; G toggles it (handled by <see cref="SystemView"/> for every
/// globe at once).
/// </summary>
public partial class PlanetSurface : MeshInstance3D
{
    // Samples per calibration lookup table: about 0.09° of latitude and 0.18° of longitude
    // apart, blended smoothly by the GPU in between.
    private const int TableSamples = 2048;

    // The far-away copy of the terrain: each face averaged over FarBlock × FarBlock cells.
    // Must match TERRAIN_FAR_SIZE in planet.gdshader.
    private const int FarSize = 256;
    private const int FarBlock = TerrainGrid.FaceSize / FarSize;

    // The warp lookup atlas: one WarpLookup tile per piece slot, 8 across and 4 down.
    private const int WarpTilesAcross = 8;
    private const int WarpTilesDown = SurfaceSettings.MaxPieces / WarpTilesAcross;

    private ShaderMaterial? _surfaceMaterial;
    private ImageTexture? _latitudeTable;
    private ImageTexture? _longitudeTable;

    // The atlas's contents (4 floats per cell), and which lookup each tile holds, so only
    // changed tiles are rewritten.
    private readonly float[] _warpAtlas = new float[
        WarpTilesAcross * WarpTilesDown * WarpLookup.Size * WarpLookup.Size * 4];
    private readonly WarpLookup?[] _warpTiles = new WarpLookup?[SurfaceSettings.MaxPieces];
    private ImageTexture? _warpTexture;

    // Painted terrain: the six faces as layers of one texture (only while something is
    // painted, about 6 MB), its averaged far-away copy (about 2 MB), the grid they show, and
    // the colors of the terrain codes.
    private Texture2DArray? _terrainTexture;
    private Texture2DArray? _farTexture;
    private readonly byte[] _faceCells = new byte[TerrainGrid.CellsPerFace];
    private TerrainGrid _shownTerrain = TerrainGrid.Empty;
    private ImageTexture? _terrainPalette;
    private byte[] _paletteBytes = [];

    /// <summary>True if a map image is currently applied.</summary>
    public bool HasMap { get; private set; }

    /// <summary>The map image currently wrapped onto the planet, or null.</summary>
    public Texture2D? MapTexture { get; private set; }

    /// <summary>Whether the latitude/longitude grid is drawn.</summary>
    public bool ShowGrid
    {
        get => (bool)GetParameter("show_grid");
        set => SurfaceMaterial.SetShaderParameter("show_grid", value);
    }

    /// <summary>
    /// Whether painted terrain is drawn (View ▸ Terrain). Painting still works while hidden.
    /// </summary>
    public bool ShowTerrain
    {
        get => (bool)GetParameter("show_terrain");
        set => SurfaceMaterial.SetShaderParameter("show_terrain", value);
    }

    /// <summary>
    /// How the map image wraps onto the globe (VISION.md MAP-03). Changing it takes effect
    /// immediately, with no reload.
    /// </summary>
    public MapProjection Projection
    {
        get => (MapProjection)(int)GetParameter("projection");
        set => SurfaceMaterial.SetShaderParameter("projection", (int)value);
    }

    /// <summary>
    /// Color used where the map doesn't cover the globe, e.g. a flat map's polar caps or a polar
    /// map's southern hemisphere (VISION.md MAP-03, MAP-04). Map types that cover the whole
    /// globe ignore it.
    /// </summary>
    public Color FillColor
    {
        get => (Color)GetParameter("fill_color");
        set => SurfaceMaterial.SetShaderParameter("fill_color", value);
    }

    // Looked up on first use rather than in _Ready, so a surface can be set up right after
    // it's created.
    private ShaderMaterial SurfaceMaterial => _surfaceMaterial ??=
        GetActiveMaterial(0) as ShaderMaterial ?? throw new InvalidOperationException(
            "PlanetSurface needs a ShaderMaterial using planet.gdshader.");

    /// <summary>
    /// Applies a grid calibration (VISION.md MAP-05), or removes it with null. Cheap enough to
    /// call on every mouse movement while dragging a guide line.
    /// </summary>
    public void SetCalibration(MapCalibration? calibration)
    {
        if (calibration is null)
        {
            SurfaceMaterial.SetShaderParameter("has_calibration", false);
            return;
        }

        _latitudeTable = UpdateTable(_latitudeTable, calibration.BakeLatitudeTable(TableSamples));
        _longitudeTable =
            UpdateTable(_longitudeTable, calibration.BakeLongitudeTable(TableSamples));
        SurfaceMaterial.SetShaderParameter("calibration_latitudes", _latitudeTable);
        SurfaceMaterial.SetShaderParameter("calibration_longitudes", _longitudeTable);
        SurfaceMaterial.SetShaderParameter("has_calibration", true);
    }

    /// <summary>
    /// Shows map pieces on the planet (VISION.md MAP-02), bottom to top, each with its warp
    /// lookup if it's warped. Only the first <see cref="SurfaceSettings.MaxPieces"/> are drawn.
    /// Cheap to call whenever a piece moves.
    /// </summary>
    public void SetPieces(
        IReadOnlyList<(Texture2D Texture, PieceProjection Projection, WarpLookup? Warp)> pieces)
    {
        const int slots = SurfaceSettings.MaxPieces;
        int count = Math.Min(pieces.Count, slots);
        var textures = new Godot.Collections.Array();
        var centers = new Vector3[slots];
        var easts = new Vector3[slots];
        var norths = new Vector3[slots];
        var frames = new Vector4[slots];
        var limits = new Vector2[slots];
        var warpTiles = new int[slots];
        var warpRects = new Vector4[slots];
        bool atlasChanged = false;

        for (int i = 0; i < count; i++)
        {
            (Texture2D texture, PieceProjection projection, WarpLookup? warp) = pieces[i];
            textures.Add(texture);
            centers[i] = projection.CenterDirection.ToGodot();
            easts[i] = projection.EastDirection.ToGodot();
            norths[i] = projection.NorthDirection.ToGodot();
            frames[i] = new Vector4(
                (float)projection.CosRotation,
                (float)projection.SinRotation,
                (float)(1.0 / projection.WidthRadians),
                (float)(1.0 / projection.HeightRadians));
            double reach = warp is null
                ? projection.ReachRadians
                : Math.Max(projection.ReachRadians,
                    warp.ReachRadians(projection.WidthRadians, projection.HeightRadians));
            limits[i] = new Vector2(
                (float)Math.Cos(Math.Min(reach, Math.PI)),
                (float)(projection.WidthRadians / Math.Max(texture.GetWidth(), 1)));

            warpTiles[i] = warp is null ? -1 : i;
            if (warp is not null)
            {
                warpRects[i] = new Vector4(
                    (float)warp.MinU, (float)warp.MinV, (float)warp.MaxU, (float)warp.MaxV);
                atlasChanged |= WriteWarpTile(i, warp);
            }
        }

        for (int i = count; i < slots; i++)
        {
            warpTiles[i] = -1;
        }

        if (atlasChanged || _warpTexture is null)
        {
            UploadWarpAtlas();
        }

        SurfaceMaterial.SetShaderParameter("piece_warp_tiles", warpTiles);
        SurfaceMaterial.SetShaderParameter("piece_warp_rects", warpRects);

        SurfaceMaterial.SetShaderParameter("piece_textures", textures);
        SurfaceMaterial.SetShaderParameter("piece_centers", centers);
        SurfaceMaterial.SetShaderParameter("piece_easts", easts);
        SurfaceMaterial.SetShaderParameter("piece_norths", norths);
        SurfaceMaterial.SetShaderParameter("piece_frames", frames);
        SurfaceMaterial.SetShaderParameter("piece_limits", limits);
        SurfaceMaterial.SetShaderParameter("piece_count", count);
    }

    /// <summary>
    /// Shows the body's painted terrain (VISION.md BOD-05). Only the faces that changed since the
    /// last call are sent to the graphics card, so it's cheap to call on every brush movement.
    /// </summary>
    public void SetTerrain(TerrainGrid terrain)
    {
        if (ReferenceEquals(terrain, _shownTerrain))
        {
            return;
        }

        if (terrain.IsEmpty)
        {
            // Nothing painted: free the textures' memory.
            SurfaceMaterial.SetShaderParameter("has_terrain", false);
            SurfaceMaterial.SetShaderParameter("terrain_cells", default);
            SurfaceMaterial.SetShaderParameter("terrain_far", default);
            _terrainTexture = null;
            _farTexture = null;
        }
        else if (_terrainTexture is null || _farTexture is null)
        {
            var faces = new Godot.Collections.Array<Image>();
            var farFaces = new Godot.Collections.Array<Image>();
            for (int face = 0; face < CubeSphere.FaceCount; face++)
            {
                faces.Add(FaceImage(terrain, face));
                farFaces.Add(FarFaceImage());
            }

            _terrainTexture = new Texture2DArray();
            _terrainTexture.CreateFromImages(faces);
            _farTexture = new Texture2DArray();
            _farTexture.CreateFromImages(farFaces);
            SurfaceMaterial.SetShaderParameter("terrain_cells", _terrainTexture);
            SurfaceMaterial.SetShaderParameter("terrain_far", _farTexture);
            SurfaceMaterial.SetShaderParameter("has_terrain", true);
        }
        else
        {
            foreach (int face in terrain.FacesChangedFrom(_shownTerrain))
            {
                _terrainTexture.UpdateLayer(FaceImage(terrain, face), face);
                _farTexture.UpdateLayer(FarFaceImage(), face);
            }
        }

        _shownTerrain = terrain;
    }

    /// <summary>Sets the color each terrain code is drawn in (others stay unpainted).</summary>
    public void SetTerrainColors(IEnumerable<TerrainType> types)
    {
        // One RGBA pixel per code; alpha 0 (the default) draws as unpainted.
        var bytes = new byte[(byte.MaxValue + 1) * 4];
        foreach (TerrainType type in types)
        {
            int at = type.Code * 4;
            (bytes[at], bytes[at + 1], bytes[at + 2], bytes[at + 3]) =
                (type.Color.R, type.Color.G, type.Color.B, 255);
        }

        if (bytes.AsSpan().SequenceEqual(_paletteBytes))
        {
            return;
        }

        _paletteBytes = bytes;
        if (_farTexture is not null)
        {
            // The averaged copy holds colors, so it's redone in the new ones.
            for (int face = 0; face < CubeSphere.FaceCount; face++)
            {
                _shownTerrain.CopyFace(face, _faceCells);
                _farTexture.UpdateLayer(FarFaceImage(), face);
            }
        }

        Image image = Image.CreateFromData(byte.MaxValue + 1, 1, false, Image.Format.Rgba8, bytes);
        if (_terrainPalette is null)
        {
            _terrainPalette = ImageTexture.CreateFromImage(image);
            SurfaceMaterial.SetShaderParameter("terrain_palette", _terrainPalette);
        }
        else
        {
            _terrainPalette.Update(image);
        }
    }

    /// <summary>
    /// How the surface looks where there's no map (VISION.md BOD-06): its color and pattern.
    /// </summary>
    /// <param name="color">The base color.</param>
    /// <param name="pattern">The pattern drawn over it.</param>
    /// <param name="bodyId">Gives the body its own version of the pattern.</param>
    public void SetAppearance(Color color, SurfacePattern pattern, Guid bodyId)
    {
        SurfaceMaterial.SetShaderParameter("base_color", color);
        SurfaceMaterial.SetShaderParameter("surface_pattern", (int)pattern);
        SurfaceMaterial.SetShaderParameter("pattern_noise", PatternNoise.Texture);
        byte[] seed = bodyId.ToByteArray();
        SurfaceMaterial.SetShaderParameter("pattern_offset",
            new Vector3(seed[0], seed[1], seed[2]) * 0.37f);
    }

    /// <summary>Wraps a map texture onto the planet and hides the grid.</summary>
    public void SetMap(Texture2D texture)
    {
        SurfaceMaterial.SetShaderParameter("surface_map", texture);
        SurfaceMaterial.SetShaderParameter(
            "map_aspect", (float)texture.GetWidth() / texture.GetHeight());
        SurfaceMaterial.SetShaderParameter("has_map", true);
        MapTexture = texture;
        HasMap = true;
        ShowGrid = false;
    }

    /// <summary>Removes the map, freeing its memory, and shows the grid again.</summary>
    public void ClearMap()
    {
        SurfaceMaterial.SetShaderParameter("surface_map", default);
        SurfaceMaterial.SetShaderParameter("has_map", false);
        SetCalibration(null);
        MapTexture = null;
        HasMap = false;
        ShowGrid = true;
    }

    // One face of a terrain grid as a one-byte-per-cell image. Leaves the face's cells in
    // _faceCells, for FarFaceImage.
    private Image FaceImage(TerrainGrid terrain, int face)
    {
        terrain.CopyFace(face, _faceCells);
        return Image.CreateFromData(
            TerrainGrid.FaceSize, TerrainGrid.FaceSize, false, Image.Format.R8, _faceCells);
    }

    // The face in _faceCells, averaged over blocks of cells into colors (multiplied by how
    // much of the block is painted), with mipmaps for ever farther views.
    private Image FarFaceImage()
    {
        var colors = new byte[FarSize * FarSize * 4];
        bool hasPalette = _paletteBytes.Length > 0;
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
                        int at = _faceCells[start + column] * 4;
                        int cellAlpha = _paletteBytes[at + 3];
                        red += _paletteBytes[at] * cellAlpha / 255;
                        green += _paletteBytes[at + 1] * cellAlpha / 255;
                        blue += _paletteBytes[at + 2] * cellAlpha / 255;
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

    // Copies a lookup into its tile of the atlas, unless that tile already holds it.
    private bool WriteWarpTile(int tile, WarpLookup warp)
    {
        if (ReferenceEquals(_warpTiles[tile], warp))
        {
            return false;
        }

        const int size = WarpLookup.Size;
        int atlasWidth = WarpTilesAcross * size;
        int left = tile % WarpTilesAcross * size;
        int top = tile / WarpTilesAcross * size;
        ReadOnlySpan<float> values = warp.Values;
        for (int row = 0; row < size; row++)
        {
            values.Slice(row * size * 4, size * 4)
                .CopyTo(_warpAtlas.AsSpan(((top + row) * atlasWidth + left) * 4, size * 4));
        }

        _warpTiles[tile] = warp;
        return true;
    }

    private void UploadWarpAtlas()
    {
        byte[] bytes = new byte[_warpAtlas.Length * sizeof(float)];
        Buffer.BlockCopy(_warpAtlas, 0, bytes, 0, bytes.Length);
        Image image = Image.CreateFromData(WarpTilesAcross * WarpLookup.Size,
            WarpTilesDown * WarpLookup.Size, false, Image.Format.Rgbaf, bytes);
        if (_warpTexture is null)
        {
            _warpTexture = ImageTexture.CreateFromImage(image);
            SurfaceMaterial.SetShaderParameter("piece_warp_atlas", _warpTexture);
        }
        else
        {
            _warpTexture.Update(image);
        }
    }

    // Writes a table of 32-bit floats into a one-pixel-tall texture, reusing the existing
    // texture when there is one (much cheaper while dragging).
    private static ImageTexture UpdateTable(ImageTexture? texture, float[] values)
    {
        byte[] bytes = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        Image image = Image.CreateFromData(values.Length, 1, false, Image.Format.Rf, bytes);
        if (texture is null)
        {
            return ImageTexture.CreateFromImage(image);
        }

        texture.Update(image);
        return texture;
    }

    // A material only stores parameters that have been set. Until then, the shader uses the
    // default written in planet.gdshader, but the material reports "nothing" (which would read
    // as false, 0, or black). Fall back to the shader's default so reads match what's drawn.
    private Variant GetParameter(string name)
    {
        Variant value = SurfaceMaterial.GetShaderParameter(name);
        return value.VariantType != Variant.Type.Nil
            ? value
            : RenderingServer.ShaderGetParameterDefault(SurfaceMaterial.Shader.GetRid(), name);
    }
}
