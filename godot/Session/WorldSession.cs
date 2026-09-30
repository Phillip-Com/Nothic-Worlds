using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Storage;
using NothicWorlds.Interop;
using NothicWorlds.Maps;
using NothicWorlds.Rendering;

namespace NothicWorlds.Session;

/// <summary>
/// The open world (VISION.md SAV-01). It holds the Core <see cref="World"/> and where its assets
/// come from, shows it on the planet and camera, tracks unsaved changes, and saves and opens
/// <c>.nworld</c> files. <b>Every edit to the world goes through here</b>, so the saved data
/// always matches what's on screen and unsaved changes are always tracked.
/// </summary>
/// <remarks>
/// Moving the camera doesn't count as an unsaved change (it would make the warning appear
/// constantly), but the current view is included whenever the world is saved.
/// </remarks>
public partial class WorldSession : Node
{
    private Dictionary<string, IAssetSource> _assets = [];

    // Each piece's baked texture, and the most recently decoded source image (for cutting).
    private Dictionary<Guid, Texture2D> _pieceTextures = [];
    private (string Name, Image Image)? _decodedSource;

    // The map type picked while no map is loaded; the next import uses it.
    private MapProjection _projectionWithoutMap = MapProjection.Mercator;

    // Increases with every edit, so a save only clears "unsaved" if nothing changed meanwhile.
    private int _editVersion;

    /// <summary>Raised when anything shown about the world changes (name, file, unsaved state,
    /// busy state, or contents).</summary>
    public event Action? Changed;

    /// <summary>Raised when a world is closed (replaced or the app quits), with its ID.</summary>
    public event Action<Guid>? WorldClosed;

    /// <summary>Raised after a successful save, with the world's ID.</summary>
    public event Action<Guid>? Saved;

    /// <summary>Draws the planet's surface.</summary>
    [Export] public PlanetSurface? Surface { get; set; }

    /// <summary>The camera, whose view is saved with the world.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>The open world. Edit it only through this class's methods.</summary>
    public World World { get; private set; } = World.CreateNew();

    /// <summary>Where the world is saved, or null if it never has been.</summary>
    public string? FilePath { get; private set; }

    /// <summary>True if the world has changes that aren't saved.</summary>
    public bool HasUnsavedChanges { get; private set; }

    /// <summary>True while a save or open is in progress.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>The size check of the map currently shown, or null if there's no map.</summary>
    public MapImageCheck? MapCheck { get; private set; }

    /// <summary>
    /// Where each of the world's assets can be read from (e.g. for recovery copies).
    /// </summary>
    public IReadOnlyDictionary<string, IAssetSource> Assets => _assets;

    /// <summary>The planet's current map type.</summary>
    public MapProjection Projection => MainBody.Surface.Map?.Projection ?? _projectionWithoutMap;

    /// <summary>The planet's fill color.</summary>
    public RgbColor FillColor => MainBody.Surface.FillColor;

    /// <summary>The current map's grid calibration (VISION.md MAP-05), or null.</summary>
    public MapCalibration? Calibration => MainBody.Surface.Map?.Calibration;

    private Body MainBody => World.Bodies[0];

    public override void _Ready()
    {
        ShowSurfaceSettings();
    }

    /// <summary>Replaces the open world with a new, empty one. Unsaved changes are discarded,
    /// so ask the user first.</summary>
    public void NewWorld()
    {
        CloseCurrentWorld();
        World = World.CreateNew();
        _assets = [];
        FilePath = null;
        MapCheck = null;
        HasUnsavedChanges = false;
        _projectionWithoutMap = MapProjection.Mercator;
        _pieceTextures = [];
        ReleaseDecodedSource();
        Surface?.ClearMap();
        ShowSurfaceSettings();
        ShowPieces();
        Camera?.SetView(null);
        Changed?.Invoke();
    }

    /// <summary>
    /// Opens a world file, replacing the open world (unsaved changes are discarded, so ask the
    /// user first). If the file can't be read, the current world stays open.
    /// </summary>
    /// <returns>
    /// A warning if the world opened but its map couldn't be shown; otherwise null.
    /// </returns>
    /// <exception cref="WorldFileException">The file couldn't be opened.</exception>
    public async Task<string?> OpenAsync(string path)
    {
        return await LoadAndShowAsync(Path.GetFullPath(path), savedPath: Path.GetFullPath(path),
            unsaved: false);
    }

    /// <summary>
    /// Opens a recovery copy after a crash (VISION.md SAV-02). The world is marked unsaved and
    /// keeps its original file location (if it had one), so the user's next save goes there.
    /// </summary>
    /// <exception cref="WorldFileException">The recovery copy couldn't be opened.</exception>
    public async Task<string?> RecoverAsync(RecoveryEntry entry)
    {
        return await LoadAndShowAsync(entry.RecoveryPath, entry.OriginalPath, unsaved: true);
    }

    /// <summary>
    /// Saves the world to <paramref name="path"/>, with the current camera view. The world's
    /// name follows the file name. Saving runs in the background, so the app stays responsive.
    /// </summary>
    /// <exception cref="WorldFileException">The world couldn't be saved; the file on disk (if
    /// any) is untouched.</exception>
    public async Task SaveAsync(string path)
    {
        RequireIdle();
        string fullPath = Path.GetFullPath(path);
        World.Name = Path.GetFileNameWithoutExtension(fullPath);
        World.ModifiedUtc = DateTimeOffset.UtcNow;
        World.View = Camera?.GetView();
        World snapshot = World.Clone();
        var assets = new Dictionary<string, IAssetSource>(_assets);
        int versionAtSave = _editVersion;

        SetBusy(true);
        try
        {
            await Task.Run(() => WorldPackage.Save(fullPath, snapshot, assets));
        }
        finally
        {
            SetBusy(false);
        }

        FilePath = fullPath;
        HasUnsavedChanges = _editVersion != versionAtSave;

        // The saved assets now live in the world file, so later saves no longer depend on the
        // original image files (which the user may move or delete). Only images the world
        // uses are saved; an image imported for cutting but not used yet stays on disk.
        foreach (string name in WorldPackage.ReferencedAssetNames(snapshot)
            .Where(_assets.ContainsKey))
        {
            _assets[name] = new PackageAssetSource(fullPath, name);
        }

        Saved?.Invoke(World.Id);
        Changed?.Invoke();
    }

    /// <summary>
    /// Imports a map image and wraps it onto the planet with the current map type.
    /// </summary>
    /// <exception cref="MapLoadException">The image couldn't be used.</exception>
    public async Task<LoadedMap> ImportMapAsync(string imagePath)
    {
        RequireIdle();
        SetBusy(true);
        LoadedMap map;
        try
        {
            map = await MapImageLoader.LoadAsync(imagePath);
        }
        finally
        {
            SetBusy(false);
        }

        string assetName = WorldPackage.CreateAssetName(Path.GetExtension(imagePath));
        _assets[assetName] = new FileAssetSource(Path.GetFullPath(imagePath));
        MainBody.Surface.Map = new SurfaceMap { AssetName = assetName, Projection = Projection };
        MapCheck = map.Check;
        Surface?.SetMap(map.Texture);
        Surface?.SetCalibration(null);  // A new map starts uncalibrated.
        MarkChanged();
        return map;
    }

    /// <summary>Removes the planet's map.</summary>
    public void ClearMap()
    {
        if (MainBody.Surface.Map is null)
        {
            return;
        }

        _projectionWithoutMap = Projection;
        MainBody.Surface.Map = null;
        MapCheck = null;
        Surface?.ClearMap();
        MarkChanged();
    }

    /// <summary>Changes the map type. Without a map, it's remembered for the next import.</summary>
    public void SetProjection(MapProjection projection)
    {
        if (projection == Projection)
        {
            return;
        }

        if (MainBody.Surface.Map is SurfaceMap map)
        {
            map.Projection = projection;
            MarkChanged();
        }
        else
        {
            // Not world data until there's a map, so it isn't an unsaved change.
            _projectionWithoutMap = projection;
            Changed?.Invoke();
        }

        ShowSurfaceSettings();
    }

    /// <summary>
    /// Replaces the map's grid calibration (VISION.md MAP-05), or removes it with null. Does
    /// nothing if there's no map. Cheap enough to call on every mouse movement while dragging.
    /// </summary>
    public void SetCalibration(MapCalibration? calibration)
    {
        if (MainBody.Surface.Map is not SurfaceMap map
            || ReferenceEquals(map.Calibration, calibration))
        {
            return;
        }

        map.Calibration = calibration;
        Surface?.SetCalibration(calibration);
        MarkChanged();
    }

    /// <summary>
    /// True while the Calibrate workspace is open (New and Open wait until it closes).
    /// </summary>
    public bool IsCalibrating { get; private set; }

    /// <summary>
    /// Starts calibrating: remembers the calibration before editing, so it can be cancelled.
    /// </summary>
    public CalibrationSnapshot BeginCalibration()
    {
        IsCalibrating = true;
        return new CalibrationSnapshot(Calibration, HasUnsavedChanges);
    }

    /// <summary>Finishes calibrating (after Done or Cancel).</summary>
    public void EndCalibration()
    {
        IsCalibrating = false;
    }

    /// <summary>
    /// Puts back the calibration from <paramref name="snapshot"/>, and the unsaved state from
    /// before editing (the workspace blocks other edits meanwhile, so nothing else changed).
    /// </summary>
    public void CancelCalibration(CalibrationSnapshot snapshot)
    {
        if (MainBody.Surface.Map is not SurfaceMap map)
        {
            return;
        }

        map.Calibration = snapshot.Calibration;
        Surface?.SetCalibration(snapshot.Calibration);
        _editVersion++;
        HasUnsavedChanges = snapshot.WasUnsaved;
        Changed?.Invoke();
    }

    /// <summary>
    /// True while the Cut editor is open. Set by the editor, so New/Open can wait until it closes.
    /// </summary>
    public bool IsCutting { get; set; }

    /// <summary>The planet's map pieces, bottom to top (VISION.md MAP-02). Read only.</summary>
    public IReadOnlyList<MapPiece> Pieces => MainBody.Surface.Pieces;

    /// <summary>The main map's asset name, for cutting pieces from it, or null.</summary>
    public string? MainMapAssetName => MainBody.Surface.Map?.AssetName;

    /// <summary>
    /// Registers an image file as a source for cutting pieces and returns its asset name. It's
    /// saved with the world only once a piece uses it.
    /// </summary>
    /// <exception cref="MapLoadException">The file can't be used.</exception>
    public string AddPieceSource(string imagePath)
    {
        MapImageLoader.ValidateFile(imagePath);
        string assetName = WorldPackage.CreateAssetName(Path.GetExtension(imagePath));
        _assets[assetName] = new FileAssetSource(Path.GetFullPath(imagePath));
        return assetName;
    }

    /// <summary>
    /// Decodes a source image at full resolution for cutting, keeping the most recent one in
    /// memory so several cuts from the same image don't decode it again. Don't dispose it.
    /// </summary>
    /// <exception cref="MapLoadException">The image can't be read.</exception>
    public async Task<Image> GetPieceSourceAsync(string assetName)
    {
        if (_decodedSource is { Name: var name, Image: var cached } && name == assetName)
        {
            return cached;
        }

        Image image = await MapImageLoader.DecodeAsync(_assets[assetName], assetName);
        ReleaseDecodedSource();
        _decodedSource = (assetName, image);
        return image;
    }

    /// <summary>Frees the source image kept for cutting (it can be hundreds of MB).</summary>
    public void ReleaseDecodedSource()
    {
        _decodedSource?.Image.Dispose();
        _decodedSource = null;
    }

    /// <summary>
    /// Cuts a new piece from a source image and lays it on the globe, on top of the others.
    /// </summary>
    /// <exception cref="InvalidOperationException">The planet has the most pieces.</exception>
    /// <exception cref="MapLoadException">The source image can't be read.</exception>
    public async Task<MapPiece> AddPieceAsync(
        string assetName, PieceOutline outline, GeoCoordinate center, double widthDegrees)
    {
        RequireIdle();
        if (Pieces.Count >= SurfaceSettings.MaxPieces)
        {
            throw new InvalidOperationException(
                $"A planet can have up to {SurfaceSettings.MaxPieces} pieces.");
        }

        SetBusy(true);
        Texture2D texture;
        try
        {
            Image source = await GetPieceSourceAsync(assetName);
            using Image baked = await Task.Run(() => PieceTextureBaker.Bake(source, outline));
            texture = ImageTexture.CreateFromImage(baked);
        }
        finally
        {
            SetBusy(false);
        }

        var piece = new MapPiece
        {
            Name = NextPieceName(),
            AssetName = assetName,
            Outline = outline,
            Center = center,
            WidthDegrees = Math.Clamp(widthDegrees,
                PieceProjection.MinimumWidthDegrees, PieceProjection.MaximumWidthDegrees),
        };
        MainBody.Surface.Pieces.Add(piece);
        _pieceTextures[piece.Id] = texture;
        ShowPieces();
        MarkChanged();
        return piece;
    }

    /// <summary>Moves, turns, or resizes a piece. Cheap enough to call while dragging.</summary>
    public void PlacePiece(
        Guid id, GeoCoordinate center, double rotationDegrees, double widthDegrees)
    {
        if (FindPiece(id) is not MapPiece piece)
        {
            return;
        }

        piece.Center = center;
        piece.RotationDegrees = ((rotationDegrees % 360) + 360) % 360;
        piece.WidthDegrees = Math.Clamp(widthDegrees,
            PieceProjection.MinimumWidthDegrees, PieceProjection.MaximumWidthDegrees);
        ShowPieces();
        MarkChanged();
    }

    /// <summary>Renames a piece. Empty names are ignored.</summary>
    public void RenamePiece(Guid id, string name)
    {
        if (FindPiece(id) is MapPiece piece && !string.IsNullOrWhiteSpace(name)
            && piece.Name != name.Trim())
        {
            piece.Name = name.Trim();
            MarkChanged();
        }
    }

    /// <summary>Removes a piece from the planet.</summary>
    public void RemovePiece(Guid id)
    {
        if (FindPiece(id) is not MapPiece piece)
        {
            return;
        }

        MainBody.Surface.Pieces.Remove(piece);
        _pieceTextures.Remove(id);
        ShowPieces();
        MarkChanged();
    }

    /// <summary>
    /// Moves a piece up (positive) or down (negative) in the layer order; higher pieces cover
    /// lower ones.
    /// </summary>
    public void MovePieceInOrder(Guid id, int steps)
    {
        List<MapPiece> pieces = MainBody.Surface.Pieces;
        int index = pieces.FindIndex(p => p.Id == id);
        int target = Math.Clamp(index + steps, 0, pieces.Count - 1);
        if (index < 0 || target == index)
        {
            return;
        }

        MapPiece piece = pieces[index];
        pieces.RemoveAt(index);
        pieces.Insert(target, piece);
        ShowPieces();
        MarkChanged();
    }

    private MapPiece? FindPiece(Guid id) => MainBody.Surface.Pieces.Find(p => p.Id == id);

    private string NextPieceName()
    {
        int number = 1;
        while (Pieces.Any(p => p.Name == $"Piece {number}"))
        {
            number++;
        }

        return $"Piece {number}";
    }

    // Sends every piece that has a texture to the planet, bottom to top.
    private void ShowPieces()
    {
        Surface?.SetPieces([.. Pieces
            .Where(p => _pieceTextures.ContainsKey(p.Id))
            .Select(p => (_pieceTextures[p.Id], PieceProjection.For(p)))]);
    }

    /// <summary>Changes the color used where the map doesn't cover the globe.</summary>
    public void SetFillColor(RgbColor color)
    {
        if (color == FillColor)
        {
            return;
        }

        MainBody.Surface.FillColor = color;
        ShowSurfaceSettings();
        MarkChanged();
    }

    /// <summary>
    /// Call when the app is quitting, after the user has saved or chosen to discard.
    /// </summary>
    public void Close()
    {
        CloseCurrentWorld();
    }

    private async Task<string?> LoadAndShowAsync(string readPath, string? savedPath, bool unsaved)
    {
        RequireIdle();
        SetBusy(true);
        LoadedWorld loaded;
        LoadedMap? map = null;
        string? warning = null;
        Dictionary<Guid, Texture2D> pieceTextures;
        try
        {
            loaded = await Task.Run(() => WorldPackage.Load(readPath));

            // If the map image can't be shown, still open the world and keep the image data,
            // so saving never loses it.
            if (loaded.World.Bodies[0].Surface.Map is SurfaceMap surfaceMap)
            {
                try
                {
                    map = await MapImageLoader.LoadAsync(
                        loaded.Assets[surfaceMap.AssetName], surfaceMap.AssetName);
                }
                catch (MapLoadException error)
                {
                    warning = $"Its map image couldn't be shown: {error.Message}";
                }
            }

            (pieceTextures, int failed) = await BakePiecesAsync(
                loaded.World.Bodies[0].Surface.Pieces, loaded.Assets);
            if (failed > 0)
            {
                warning = $"{warning} {failed} map piece(s) couldn't be shown (they're kept, " +
                    "so saving won't lose them).".Trim();
            }
        }
        finally
        {
            SetBusy(false);
        }

        CloseCurrentWorld();
        _pieceTextures = pieceTextures;
        World = loaded.World;
        _assets = new Dictionary<string, IAssetSource>(loaded.Assets);
        FilePath = savedPath;
        HasUnsavedChanges = unsaved;
        MapCheck = map?.Check;
        _projectionWithoutMap = MapProjection.Mercator;
        if (map is not null)
        {
            Surface?.SetMap(map.Texture);
        }
        else
        {
            Surface?.ClearMap();
        }

        ShowSurfaceSettings();
        ShowPieces();
        Camera?.SetView(World.View);
        _editVersion++;
        Changed?.Invoke();
        return warning;
    }

    // Bakes a texture for each piece, decoding each source image once. Pieces whose image can't
    // be read are skipped (and counted), but kept in the world.
    private static async Task<(Dictionary<Guid, Texture2D> Textures, int Failed)> BakePiecesAsync(
        IReadOnlyList<MapPiece> pieces, IReadOnlyDictionary<string, IAssetSource> assets)
    {
        var textures = new Dictionary<Guid, Texture2D>();
        int failed = 0;
        foreach (IGrouping<string, MapPiece> group in pieces.GroupBy(p => p.AssetName))
        {
            try
            {
                using Image source = await MapImageLoader.DecodeAsync(assets[group.Key], group.Key);
                foreach (MapPiece piece in group)
                {
                    using Image baked =
                        await Task.Run(() => PieceTextureBaker.Bake(source, piece.Outline));
                    textures[piece.Id] = ImageTexture.CreateFromImage(baked);
                }
            }
            catch (MapLoadException)
            {
                failed += group.Count();
            }
        }

        return (textures, failed);
    }

    private void ShowSurfaceSettings()
    {
        if (Surface is null)
        {
            return;
        }

        Surface.Projection = Projection;
        Surface.FillColor = FillColor.ToGodot();
        Surface.SetCalibration(Calibration);
    }

    private void MarkChanged()
    {
        _editVersion++;
        HasUnsavedChanges = true;
        Changed?.Invoke();
    }

    private void CloseCurrentWorld()
    {
        WorldClosed?.Invoke(World.Id);
    }

    private void SetBusy(bool isBusy)
    {
        IsBusy = isBusy;
        Changed?.Invoke();
    }

    private void RequireIdle()
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("Wait for the current save or open to finish.");
        }
    }
}
