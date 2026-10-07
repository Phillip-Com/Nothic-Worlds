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
    // At most this often (seconds), the averaged terrain copy is redone in new colors.
    private const double FarRecolorSeconds = 0.25;
    private const int TableSamples = 2048;

    // The far-away copy of the terrain: each face averaged over FarBlock × FarBlock cells.
    // Must match TERRAIN_FAR_SIZE in planet.gdshader.
    private const int FarSize = 256;
    private const int FarBlock = TerrainGrid.FaceSize / FarSize;

    // The warp lookup atlas: one WarpLookup tile per piece slot, 8 across and 4 down.
    private const int WarpTilesAcross = 8;
    private const int WarpTilesDown = SurfaceSettings.MaxPieces / WarpTilesAcross;

    // The surface shader for each visual style (VISION.md REN-05). Realistic is the one the
    // scene's material starts with.
    private static readonly Dictionary<VisualStyle, Shader> _styleShaders = new()
    {
        [VisualStyle.Painterly] = GD.Load<Shader>("res://Rendering/planet_painterly.gdshader"),
        [VisualStyle.Realistic] = GD.Load<Shader>("res://Rendering/planet.gdshader"),
        [VisualStyle.Simple] = GD.Load<Shader>("res://Rendering/planet_simple.gdshader"),
    };

    private ShaderMaterial? _surfaceMaterial;
    private VisualStyle _style = VisualStyle.Realistic;
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

    // The averaged terrain copy is waiting to be redone in new colors, and how long since it
    // last was.
    private bool _farColorsStale;
    private double _sinceFarRedone;
    private readonly byte[] _faceCells = new byte[TerrainGrid.CellsPerFace];
    private TerrainGrid _shownTerrain = TerrainGrid.Empty;
    private ImageTexture? _terrainPalette;
    private byte[] _paletteBytes = [];
    private ImageTexture? _waterPalette;
    private byte[] _waterPaletteBytes = [];

    // Sculpted heights (VISION.md BOD-04): the six faces as layers of one texture of
    // half-precision floats with smaller copies (only while something is sculpted, about 16 MB),
    // the grid they show, and how far a meter lifts the unit sphere (with the exaggeration).
    // Half precision rounds the drawn heights by at most 16 m (at 32 km); the saved ones are
    // exact.
    private Texture2DArray? _heightTexture;
    private readonly byte[] _faceHeightBytes = new byte[HeightGrid.CellsPerFace * 2];
    private readonly short[] _faceHeights = new short[HeightGrid.CellsPerFace];
    private HeightGrid _shownHeights = HeightGrid.Empty;
    private float _reliefScale;
    private ReliefDetail _reliefDetail = ReliefDetail.Standard;

    // The body's water level in meters (VISION.md BOD-09), or null for none.
    private int? _waterLevelMeters;

    // Shapes added or cut (VISION.md BOD-04): the body's shapes and radius, and the carved
    // globe drawn instead of the plain one while there are any.
    private IReadOnlyList<ShapeEdit> _shapes = [];
    private double _radiusKm = 6371;
    private ShapedGlobe? _carved;
    private bool _mapShading;

    // The globe mesh, kept while the body is flat, and the flat world's rock.
    private Mesh? _sphereMesh;

    // Level of detail (VISION.md REN-03; owner's choice: always on): a globe this small on
    // screen (its radius in pixels) is drawn with a lighter mesh. Each level is left a little
    // past where it's entered, so a globe at the boundary doesn't flicker between them.
    private const float LightBelowPixels = 200;
    private const float PlainBelowPixels = 60;
    private const float LevelMarginPixels = 1.15f;

    // A coarse sphere for globes too small to show the fine one's roundness.
    private static readonly SphereMesh _coarseSphere = new()
    {
        Radius = 1.0f,
        Height = 2.0f,
        RadialSegments = 32,
        Rings = 16,
    };

    private DetailLevel _detailLevel = DetailLevel.Full;

    private enum DetailLevel
    {
        Full,     // The chosen relief detail and the fine sphere
        Light,    // The lightest relief mesh
        Plain,    // The coarse sphere, sculpted or not (the shader still lifts the relief)
    }
    private MeshInstance3D? _rock;
    private BodyShape _shape = BodyShape.Sphere;

    private static readonly StandardMaterial3D _rockMaterial = new()
    {
        AlbedoColor = new Color(0.36f, 0.33f, 0.30f),
        Roughness = 1.0f,
    };

    /// <summary>The bare rock of a flat world's rim and underside.</summary>
    public static Material RockMaterial => _rockMaterial;

    /// <summary>
    /// The globe's shape (VISION.md BOD-02): a sphere, or a flat world's disc with bare rock
    /// underneath. Maps and terrain stay as they are; only where they're drawn changes.
    /// </summary>
    public BodyShape Shape
    {
        get => _shape;
        set
        {
            if (value == _shape)
            {
                return;
            }

            _shape = value;
            bool flat = value == BodyShape.FlatDisc;
            ChooseMesh();
            SurfaceMaterial.SetShaderParameter("flat_disc", flat);
            if (flat && _rock is null)
            {
                _rock = new MeshInstance3D
                {
                    Name = "Rock",
                    Mesh = FlatDiscMeshes.Rock,
                    MaterialOverride = _rockMaterial,
                };
                AddChild(_rock);
            }
            else if (!flat && _rock is not null)
            {
                _rock.QueueFree();
                _rock = null;
            }
        }
    }

    /// <summary>
    /// Sets the rings whose shadow falls on the surface (VISION.md BOD-03), or none, with the
    /// normal of the plane they lie in (in the planet's own space) and their pattern's seed.
    /// </summary>
    public void SetRings(PlanetRings? rings, Vector3 normal, float seed)
    {
        SurfaceMaterial.SetShaderParameter("has_rings", rings is not null);
        if (rings is not null)
        {
            SurfaceMaterial.SetShaderParameter("ring_inner", (float)rings.InnerRadii);
            SurfaceMaterial.SetShaderParameter("ring_outer", (float)rings.OuterRadii);
            SurfaceMaterial.SetShaderParameter("ring_seed", seed);
            SurfaceMaterial.SetShaderParameter("ring_normal", normal);
        }
    }

    /// <summary>
    /// The way to the star, in the planet's own space: for the rings' shadow, and for live
    /// weather, which fades by night.
    /// </summary>
    public void SetSunDirection(Vector3 direction) =>
        SurfaceMaterial.SetShaderParameter("sun_direction", direction);

    /// <summary>
    /// Draws live weather over the surface (VISION.md WTH-02): two snapshots and how far the
    /// clock is from the older to the newer (0 to 1), at a clock time (standard days). See
    /// <see cref="WeatherSnapshots"/>.
    /// </summary>
    public void ShowWeather(Texture2D weatherOld, Texture2D weatherNew, Texture2D windOld,
        Texture2D windNew, float blend, double clockDays)
    {
        ShaderMaterial material = SurfaceMaterial;
        material.SetShaderParameter("has_weather", true);
        material.SetShaderParameter("weather_old", weatherOld);
        material.SetShaderParameter("weather_new", weatherNew);
        material.SetShaderParameter("wind_old", windOld);
        material.SetShaderParameter("wind_new", windNew);
        material.SetShaderParameter("weather_blend", blend);
        material.SetShaderParameter("weather_clock", (float)(clockDays % 1000));
    }

    /// <summary>
    /// Which parts of the weather show (clouds with rain and snow, the wind), and how finely.
    /// </summary>
    public void SetWeatherLook(bool clouds, bool wind, CloudDetail detail)
    {
        ShaderMaterial material = SurfaceMaterial;
        material.SetShaderParameter("show_clouds", clouds);
        material.SetShaderParameter("show_wind", wind);
        material.SetShaderParameter("cloud_detail", detail == CloudDetail.High ? 1f : 0f);
    }

    /// <summary>
    /// Copies the live weather's clouds as drawn now (the snapshots, the blend between them,
    /// the clock, and the detail) to <paramref name="target"/>, a material with the same
    /// uniforms; false, copying nothing, if no clouds are shown.
    /// </summary>
    public bool CopyCloudsTo(ShaderMaterial target)
    {
        ShaderMaterial material = SurfaceMaterial;
        if (!material.GetShaderParameter("has_weather").AsBool()
            || !material.GetShaderParameter("show_clouds").AsBool())
        {
            return false;
        }

        foreach (string name in (string[])["weather_old", "weather_new", "weather_blend",
            "weather_clock", "cloud_detail"])
        {
            target.SetShaderParameter(name, material.GetShaderParameter(name));
        }

        return true;
    }

    /// <summary>Stops drawing live weather.</summary>
    public void HideWeather() => SurfaceMaterial.SetShaderParameter("has_weather", false);

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

    /// <summary>
    /// Shows the body's sculpted heights (VISION.md BOD-04): a sculpted globe is drawn as the
    /// cube-sphere mesh lifted by them. Only the faces that changed since the last call are sent
    /// to the graphics card, so it's cheap to call on every brush movement.
    /// </summary>
    public void SetHeights(HeightGrid heights)
    {
        if (ReferenceEquals(heights, _shownHeights))
        {
            return;
        }

        if (heights.IsEmpty)
        {
            // Nothing sculpted: back to the plain sphere, and free the texture's memory.
            SurfaceMaterial.SetShaderParameter("has_heights", false);
            SurfaceMaterial.SetShaderParameter("heights", default);
            _heightTexture = null;
        }
        else if (_heightTexture is null)
        {
            var faces = new Godot.Collections.Array<Image>();
            for (int face = 0; face < CubeSphere.FaceCount; face++)
            {
                faces.Add(HeightFaceImage(heights, face));
            }

            _heightTexture = new Texture2DArray();
            _heightTexture.CreateFromImages(faces);
            SurfaceMaterial.SetShaderParameter("heights", _heightTexture);
            SurfaceMaterial.SetShaderParameter("has_heights", true);
        }
        else
        {
            foreach (int face in heights.FacesChangedFrom(_shownHeights))
            {
                _heightTexture.UpdateLayer(HeightFaceImage(heights, face), face);
            }
        }

        _shownHeights = heights;
        ChooseMesh();
        UpdateBounds();
    }

    /// <summary>
    /// How far one meter of height lifts the surface, in the globe's radii: the view's relief
    /// exaggeration over the body's radius in meters. Only touches the shader when it changes.
    /// </summary>
    public float ReliefScale
    {
        get => _reliefScale;
        set
        {
            if (value != _reliefScale)
            {
                _reliefScale = value;
                SurfaceMaterial.SetShaderParameter("relief_scale", value);
                if (_carved is not null)
                {
                    ChooseMesh();
                }

                UpdateBounds();
            }
        }
    }

    /// <summary>
    /// The body's water level in meters (VISION.md BOD-09), or null for none: wherever the
    /// ground is lower, the globe is drawn as water, level with it. A flat world shows none.
    /// </summary>
    public int? WaterLevelMeters
    {
        get => _waterLevelMeters;
        set
        {
            if (value != _waterLevelMeters)
            {
                _waterLevelMeters = value;
                SurfaceMaterial.SetShaderParameter("has_water", value is not null);
                SurfaceMaterial.SetShaderParameter("water_level", (float)(value ?? 0));
                UpdateBounds();
            }
        }
    }

    /// <summary>
    /// How far out the water's surface is, in the globe's radii (with the view's relief
    /// exaggeration), or null with no water (or on a flat world).
    /// </summary>
    public double? WaterRadius => _waterLevelMeters is int level && Shape == BodyShape.Sphere
        ? 1 + _reliefScale * (double)level
        : null;

    /// <summary>
    /// Whether relief is shaded map-style, from a fixed direction (true), or by the sunlight.
    /// </summary>
    public bool MapShading
    {
        get => _mapShading;
        set
        {
            if (value != _mapShading)
            {
                _mapShading = value;
                SurfaceMaterial.SetShaderParameter("map_shading", value);
            }
        }
    }

    /// <summary>
    /// How the surface is drawn (VISION.md REN-05). Each style is its own shader; the
    /// material's settings carry over when it changes.
    /// </summary>
    public VisualStyle Style
    {
        get => _style;
        set
        {
            if (value != _style)
            {
                _style = value;
                SurfaceMaterial.Shader = _styleShaders[value];
            }
        }
    }

    /// <summary>
    /// How big the globe is drawn on screen, as its radius in pixels (0 if it's behind the
    /// camera): small globes get lighter meshes (level of detail, VISION.md REN-03).
    /// </summary>
    public float ScreenRadius
    {
        set
        {
            DetailLevel level = LevelFor(value);
            if (level != _detailLevel)
            {
                _detailLevel = level;
                ChooseMesh();
            }
        }
    }

    /// <summary>How finely the sculpted shape is drawn (a quality setting).</summary>
    public ReliefDetail ReliefDetail
    {
        get => _reliefDetail;
        set
        {
            if (value != _reliefDetail)
            {
                _reliefDetail = value;
                ChooseMesh();
            }
        }
    }

    /// <summary>
    /// Goes up by one whenever the drawn relief changes (the heights or the exaggeration), so
    /// whatever sits on the surface knows to move.
    /// </summary>
    public int ReliefVersion { get; private set; }

    /// <summary>
    /// Shows the shapes added to or cut out of the body (VISION.md BOD-04), on a body of
    /// <paramref name="radiusKm"/>. With any, the globe is drawn carved
    /// (<see cref="ShapedGlobe"/>).
    /// </summary>
    public void SetShapes(IReadOnlyList<ShapeEdit> shapes, double radiusKm)
    {
        if (shapes.SequenceEqual(_shapes) && radiusKm == _radiusKm)
        {
            return;
        }

        _shapes = [.. shapes];
        _radiusKm = radiusKm;
        ChooseMesh();
        UpdateBounds();
    }

    /// <summary>True while the globe is drawn carved by shapes (VISION.md BOD-04).</summary>
    public bool IsCarved => _carved is not null;

    /// <summary>
    /// Where a ray (in the globe's own space) first meets the carved globe, into its holes and
    /// hollows, or null if it misses or the globe isn't carved.
    /// </summary>
    public Vector3? CarvedHit(Vector3 origin, Vector3 direction) =>
        _carved?.RayHit(origin, direction);

    /// <summary>
    /// How far a drawn point (in the globe's own space) is above the body's radius, in true km.
    /// The ground under it is drawn with its relief exaggerated and shapes aren't, so the
    /// exaggeration there is taken back out.
    /// </summary>
    public double TrueHeightKmOf(Vector3 point, double radiusKm)
    {
        var direction = new Vector3D(point.X, point.Y, point.Z);
        double groundMeters = _shownHeights.SampleAt(direction);
        double lift = groundMeters * (_reliefScale - 1 / (radiusKm * 1000));
        return (point.Length() - 1 - lift) * radiusKm;
    }

    /// <summary>
    /// Shows a shape being dragged as a see-through preview (VISION.md BOD-04), or hides it.
    /// </summary>
    public void SetShapePreview(ShapeEdit? shape) => _carved?.ShowPreview(shape);

    /// <summary>
    /// How far out the drawn ground is, in the globe's radii, as seen up close (first person):
    /// as <see cref="SurfaceRadiusAt"/>, but with cliffs kept steep (see
    /// <see cref="HeightGrid.SampleSteepAt"/>) on a body <paramref name="radiusKm"/> in radius.
    /// </summary>
    public float GroundRadiusAt(Vector3D direction, double radiusKm) =>
        _shownHeights.IsEmpty || Shape == BodyShape.FlatDisc
            ? 1.0f
            : 1.0f + _reliefScale * (float)_shownHeights.SampleSteepAt(direction,
                radiusKm * 1000 * Math.PI / 2 / HeightGrid.FaceSize);

    /// <summary>
    /// How far out the drawn surface is at a direction, in the globe's radii: 1 on an unsculpted
    /// globe (or a flat world), more on a sculpted hill, less in a basin. Overlays sit on it.
    /// </summary>
    public float SurfaceRadiusAt(Vector3D direction)
    {
        float ground = _shownHeights.IsEmpty || Shape == BodyShape.FlatDisc
            ? 1.0f
            : 1.0f + _reliefScale * (float)_shownHeights.SampleAt(direction);
        return WaterRadius is double water ? Math.Max(ground, (float)water) : ground;
    }

    /// <summary>
    /// The highest the surface reaches anywhere, in the globe's radii above it (0 unsculpted).
    /// </summary>
    public float HighestRelief => Shape == BodyShape.FlatDisc
        ? 0
        : Math.Max(Math.Max(Math.Max(0, _reliefScale * _shownHeights.Highest),
            _reliefScale * (_waterLevelMeters ?? 0)), _carved?.HighestTop ?? 0);

    /// <summary>
    /// Sets the color each terrain code is drawn in (others stay unpainted), and the color of
    /// water over it: its own for a type with the Water climate (VISION.md BOD-09).
    /// </summary>
    public void SetTerrainColors(IEnumerable<TerrainType> types)
    {
        SetWaterColors(types);

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

        // The averaged copy holds colors, so it's redone in the new ones: soon, not now, as
        // redoing it for every step of a color being picked froze the app (see _Process).
        _farColorsStale = true;

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
    /// Gives a first-person water surface's material (water_surface.gdshader) the terrain and
    /// water colors it tints the water by.
    /// </summary>
    public void CopyWaterColorsTo(ShaderMaterial target)
    {
        ShaderMaterial material = SurfaceMaterial;
        foreach (string name in (string[])["has_terrain", "terrain_cells", "water_palette"])
        {
            target.SetShaderParameter(name, material.GetShaderParameter(name));
        }
    }

    // One RGBA pixel per code: the type's color for water types, alpha 0 for the rest.
    private void SetWaterColors(IEnumerable<TerrainType> types)
    {
        var bytes = new byte[(byte.MaxValue + 1) * 4];
        foreach (TerrainType type in types.Where(type => type.Climate == ClimateKind.Water))
        {
            int at = type.Code * 4;
            (bytes[at], bytes[at + 1], bytes[at + 2], bytes[at + 3]) =
                (type.Color.R, type.Color.G, type.Color.B, 255);
        }

        if (bytes.AsSpan().SequenceEqual(_waterPaletteBytes))
        {
            return;
        }

        _waterPaletteBytes = bytes;
        Image image = Image.CreateFromData(byte.MaxValue + 1, 1, false, Image.Format.Rgba8, bytes);
        if (_waterPalette is null)
        {
            _waterPalette = ImageTexture.CreateFromImage(image);
            SurfaceMaterial.SetShaderParameter("water_palette", _waterPalette);
        }
        else
        {
            _waterPalette.Update(image);
        }
    }

    public override void _Process(double delta)
    {
        _sinceFarRedone += delta;
        if (_farColorsStale && _sinceFarRedone >= FarRecolorSeconds)
        {
            RedoFarColors();
        }
    }

    // Redoes the averaged terrain copy in the current colors, on the faces with any painting
    // (the rest are unpainted, which no color changes).
    private void RedoFarColors()
    {
        _farColorsStale = false;
        _sinceFarRedone = 0;
        if (_farTexture is null)
        {
            return;
        }

        foreach (int face in _shownTerrain.FacesChangedFrom(TerrainGrid.Empty))
        {
            _shownTerrain.CopyFace(face, _faceCells);
            _farTexture.UpdateLayer(FarFaceImage(), face);
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
    // One face's heights as an image of half-precision floats in meters, with its smaller
    // copies, as the shader reads them.
    private Image HeightFaceImage(HeightGrid heights, int face)
    {
        heights.CopyFace(face, _faceHeights);
        for (int index = 0; index < _faceHeights.Length; index++)
        {
            BitConverter.TryWriteBytes(_faceHeightBytes.AsSpan(index * 2),
                (Half)_faceHeights[index]);
        }

        Image image = Image.CreateFromData(HeightGrid.FaceSize, HeightGrid.FaceSize, false,
            Image.Format.Rh, _faceHeightBytes);
        image.GenerateMipmaps();
        return image;
    }

    // The level of detail for a globe this big on screen; leaving a level takes a little more
    // than entering it did.
    private DetailLevel LevelFor(float pixels)
    {
        float plainUntil = PlainBelowPixels
            * (_detailLevel == DetailLevel.Plain ? LevelMarginPixels : 1);
        float lightUntil = LightBelowPixels
            * (_detailLevel >= DetailLevel.Light ? LevelMarginPixels : 1);
        return pixels < plainUntil ? DetailLevel.Plain
            : pixels < lightUntil ? DetailLevel.Light
            : DetailLevel.Full;
    }

    // A flat world's disc, a carved globe (drawn by its own child node), a sculpted globe's
    // cube-sphere, or the plain sphere, lighter when the globe is small on screen.
    private void ChooseMesh()
    {
        _sphereMesh ??= Mesh;
        bool carved = Shape == BodyShape.Sphere && _shapes.Count > 0;
        Mesh? sphere = _detailLevel == DetailLevel.Plain ? _coarseSphere : _sphereMesh;
        ReliefDetail relief = _detailLevel == DetailLevel.Light
            ? (ReliefDetail)Math.Min((int)_reliefDetail, (int)ReliefDetail.Low)
            : _reliefDetail;
        Mesh = Shape == BodyShape.FlatDisc ? FlatDiscMeshes.Top
            : carved ? null
            : _shownHeights.IsEmpty || _detailLevel == DetailLevel.Plain ? sphere
            : CubeSphereMesh.For(relief);
        SurfaceMaterial.SetShaderParameter("lifted_on_cpu", carved);
        if (carved)
        {
            _carved ??= new ShapedGlobe { Name = "Shaped" };
            if (_carved.GetParent() is null)
            {
                AddChild(_carved);
            }

            _carved.Show(_shapes, _shownHeights, _radiusKm, _reliefScale, SurfaceMaterial);
        }
        else if (_carved is not null)
        {
            _carved.QueueFree();
            _carved = null;
        }
    }

    // The engine skips drawing what's outside a mesh's bounds, and doesn't know the shader
    // lifts the ground, so the bounds grow to hold the highest peak.
    private void UpdateBounds()
    {
        ReliefVersion++;
        float reach = 1.0f + HighestRelief;
        CustomAabb = HighestRelief > 0
            ? new Aabb(-Vector3.One * reach, Vector3.One * (2 * reach))
            : default;
    }

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
