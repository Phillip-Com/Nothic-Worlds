using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Editing;
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
/// come from, shows it in the system view and camera, tracks unsaved changes, and saves and
/// opens <c>.nworld</c> files. <b>Every edit to the world goes through here</b>, so the saved
/// data always matches what's on screen and unsaved changes are always tracked.
/// </summary>
/// <remarks>
/// <para>One body is <b>selected</b> at a time (VISION.md BOD-01): the map tools (import,
/// calibrate, pieces) work on it, and the view flies to it. Only the selected body's map is
/// kept at full size; the others show a small preview, so a system of mapped planets fits the
/// baseline laptop's graphics memory.</para>
/// <para>Moving the camera, or the world clock, doesn't count as an unsaved change (it would
/// make the warning appear constantly), but both are saved with the world.</para>
/// <para>Every edit can be undone: each one records a copy of the changed body's surface first
/// (see <see cref="UndoAsync"/>).</para>
/// </remarks>
public partial class WorldSession : Node
{
    private Dictionary<string, IAssetSource> _assets = [];

    // Each piece's baked texture, and the most recently decoded source image (for cutting).
    private Dictionary<Guid, Texture2D> _pieceTextures = [];
    private (string Name, Image Image)? _decodedSource;

    // Each warped piece's baked lookup, with the warp it was baked from (rebaked when it changes).
    private readonly Dictionary<Guid, (IReadOnlyList<ImagePoint> Points, WarpLookup Lookup)>
        _warpLookups = [];

    // Maps on screen: the full-size map's body and image, each body's full-size check, and
    // each body's small preview (with the image it was made from).
    private (Guid Body, string Asset)? _fullMap;
    private readonly Dictionary<Guid, MapImageCheck> _mapChecks = [];
    private readonly Dictionary<Guid, (string Asset, Texture2D Texture)> _previews = [];

    // The map type picked while the selected body has no map; its next import uses it.
    private MapProjection _projectionWithoutMap = MapProjection.Mercator;

    // Increases with every edit, so a gesture can tell whether it changed anything.
    private int _editVersion;

    // Undo/redo: snapshots of one body's surface. While a gesture (a drag, or a calibration
    // session) is under way, its edits add up to one step, recorded when it ends.
    private readonly UndoHistory<SurfaceSnapshot> _history = new();
    private (string Description, SurfaceSnapshot Before, int Version)? _gesture;

    // Copies of images only the undo history still needs (see AssetStash).
    private AssetStash? _stash;

    // Every body's surface as it is in the saved file (or as a new world started), so the
    // world counts as saved whenever it matches again, e.g. after undoing back to it. Null when
    // there's nothing to match (a recovered world is unsaved until it's saved).
    private Dictionary<Guid, SurfaceSettings>? _savedSurfaces;

    /// <summary>Raised when anything shown about the world changes (name, file, unsaved state,
    /// busy state, selection, or contents).</summary>
    public event Action? Changed;

    /// <summary>Raised when a different body is selected (or a world opens).</summary>
    public event Action? SelectionChanged;

    /// <summary>Raised when the world clock changes (often: every frame while it runs).</summary>
    public event Action? TimeChanged;

    /// <summary>Raised when a world is closed (replaced or the app quits), with its ID.</summary>
    public event Action<Guid>? WorldClosed;

    /// <summary>Raised after a successful save, with the world's ID.</summary>
    public event Action<Guid>? Saved;

    /// <summary>Draws the star system.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The camera, whose view is saved with the world.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>The open world. Edit it only through this class's methods.</summary>
    public World World { get; private set; } = World.CreateNew();

    /// <summary>Where the world is saved, or null if it never has been.</summary>
    public string? FilePath { get; private set; }

    /// <summary>
    /// True if the world differs from its saved file. Undoing back to exactly what was saved
    /// makes it false again.
    /// </summary>
    public bool HasUnsavedChanges { get; private set; }

    /// <summary>True while a save, an open, or a map load is in progress.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>The selected body's ID (see <see cref="SelectBodyAsync"/>).</summary>
    public Guid SelectedBodyId { get; private set; }

    /// <summary>The selected body: the one the map tools work on and the view centers on.</summary>
    public Body SelectedBody => FindBody(SelectedBodyId) ?? World.Bodies[0];

    /// <summary>True if the selected body can have a map (planets and moons; not stars).</summary>
    public bool SelectedBodyHasSurface => SelectedBody.Kind != BodyKind.Star;

    /// <summary>The selected body's surface, or null for a star.</summary>
    public PlanetSurface? Surface => System?.SurfaceFor(SelectedBodyId);

    /// <summary>
    /// The size check of the selected body's map, or null if it has none (or it's still
    /// loading).
    /// </summary>
    public MapImageCheck? MapCheck => _mapChecks.GetValueOrDefault(SelectedBodyId);

    /// <summary>
    /// Where each of the world's assets can be read from (e.g. for recovery copies).
    /// </summary>
    public IReadOnlyDictionary<string, IAssetSource> Assets => _assets;

    /// <summary>The selected body's map type.</summary>
    public MapProjection Projection =>
        SelectedBody.Surface.Map?.Projection ?? _projectionWithoutMap;

    /// <summary>The selected body's fill color.</summary>
    public RgbColor FillColor => SelectedBody.Surface.FillColor;

    /// <summary>The selected body's map calibration (VISION.md MAP-05), or null.</summary>
    public MapCalibration? Calibration => SelectedBody.Surface.Map?.Calibration;

    /// <summary>The world clock, in standard days (VISION.md SIM-02).</summary>
    public double TimeDays => World.TimeDays;

    public override void _Ready()
    {
        SelectedBodyId = DefaultSelection(World);
        _savedSurfaces = SnapshotSurfaces(World);
        ShowWorld();
    }

    public override void _ExitTree()
    {
        // However the app ends, don't leave the undo copies in the temporary folder.
        _stash?.Dispose();
        _stash = null;
    }

    /// <summary>Replaces the open world with a new one: a sun and one unmapped planet.
    /// Unsaved changes are discarded, so ask the user first.</summary>
    public void NewWorld()
    {
        CloseCurrentWorld();
        World = World.CreateNew();
        _assets = [];
        FilePath = null;
        ForgetMaps();
        SelectedBodyId = DefaultSelection(World);
        HasUnsavedChanges = false;
        _savedSurfaces = SnapshotSurfaces(World);
        _projectionWithoutMap = MapProjection.Mercator;
        _pieceTextures = [];
        ResetHistory();
        ReleaseDecodedSource();
        ShowWorld();
        Camera?.SetView(null);
        SelectionChanged?.Invoke();
        TimeChanged?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>
    /// Opens a world file, replacing the open world (unsaved changes are discarded, so ask the
    /// user first). If the file can't be read, the current world stays open.
    /// </summary>
    /// <returns>
    /// A warning if the world opened but some images couldn't be shown; otherwise null.
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
        List<string> neededByHistory = AssetsOnlyInHistory(snapshot, fullPath);

        SetBusy(true);
        try
        {
            Dictionary<string, FileAssetSource> kept = await Task.Run(() =>
            {
                // Copy them out first: the save replaces the file they're in.
                Dictionary<string, FileAssetSource> copies = neededByHistory.ToDictionary(
                    name => name, name => Stash().Keep(name, assets[name]));
                WorldPackage.Save(fullPath, snapshot, assets);
                return copies;
            });
            foreach ((string name, FileAssetSource copy) in kept)
            {
                _assets[name] = copy;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new WorldFileException(
                $"Couldn't save: an image kept for undo couldn't be copied ({error.Message}).",
                error);
        }
        finally
        {
            SetBusy(false);
        }

        FilePath = fullPath;
        _savedSurfaces = SnapshotSurfaces(snapshot);
        UpdateUnsavedState();

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
    /// Selects a body: the view flies to it, and the map tools work on it from now on. Its
    /// map is loaded at full size (the previously selected body goes back to a preview).
    /// Does nothing while something else is in progress.
    /// </summary>
    /// <returns>A warning if the body's map couldn't be shown; otherwise null.</returns>
    public async Task<string?> SelectBodyAsync(Guid bodyId)
    {
        if (bodyId == SelectedBodyId || FindBody(bodyId) is not Body body || !IsIdleForHistory)
        {
            return null;
        }

        Body previous = SelectedBody;
        SelectedBodyId = bodyId;
        System?.FlyTo(bodyId);
        ShowPreviewInsteadOfFullMap(previous);
        SelectionChanged?.Invoke();
        Changed?.Invoke();
        return await ShowFullMapAsync(body);
    }

    /// <summary>
    /// Sets the world clock (VISION.md SIM-02). Every body moves and spins to match. Not an
    /// unsaved change (see the class remarks).
    /// </summary>
    public void SetTime(double timeDays)
    {
        if (double.IsFinite(timeDays) && timeDays != World.TimeDays)
        {
            World.TimeDays = timeDays;
            TimeChanged?.Invoke();
        }
    }

    /// <summary>
    /// Imports a map image and wraps it onto the selected body with the current map type.
    /// </summary>
    /// <exception cref="MapLoadException">The image couldn't be used.</exception>
    /// <exception cref="InvalidOperationException">The selected body is a star.</exception>
    public async Task<LoadedMap> ImportMapAsync(string imagePath)
    {
        RequireIdle();
        RequireSurface();
        Body body = SelectedBody;
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
        RecordUndo("Import Map");
        body.Surface.Map = new SurfaceMap { AssetName = assetName, Projection = Projection };
        _mapChecks[body.Id] = map.Check;
        _fullMap = (body.Id, assetName);
        Surface?.SetMap(map.Texture);
        Surface?.SetCalibration(null);  // A new map starts uncalibrated.
        MarkChanged();
        return map;
    }

    /// <summary>Removes the selected body's map.</summary>
    public void ClearMap()
    {
        if (SelectedBody.Surface.Map is null)
        {
            return;
        }

        RecordUndo("Clear Map");
        _projectionWithoutMap = Projection;
        SelectedBody.Surface.Map = null;
        _mapChecks.Remove(SelectedBodyId);
        _fullMap = null;
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

        if (SelectedBody.Surface.Map is SurfaceMap map)
        {
            RecordUndo("Change Map Type");
            map.Projection = projection;
            MarkChanged();
        }
        else
        {
            // Not world data until there's a map, so it isn't an unsaved change.
            _projectionWithoutMap = projection;
            Changed?.Invoke();
        }

        ShowSurfaceSettings(SelectedBody);
    }

    /// <summary>
    /// Replaces the map's grid calibration (VISION.md MAP-05), or removes it with null. Does
    /// nothing if there's no map. Cheap enough to call on every mouse movement while dragging.
    /// </summary>
    public void SetCalibration(MapCalibration? calibration)
    {
        if (SelectedBody.Surface.Map is not SurfaceMap map
            || ReferenceEquals(map.Calibration, calibration))
        {
            return;
        }

        RecordUndo("Calibrate Map");
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
        BeginGesture("Calibrate Map");
        return new CalibrationSnapshot(Calibration);
    }

    /// <summary>Finishes calibrating (after Done or Cancel).</summary>
    public void EndCalibration()
    {
        IsCalibrating = false;
        EndGesture();
    }

    /// <summary>
    /// Puts back the calibration from <paramref name="snapshot"/> (the workspace blocks other
    /// edits meanwhile, so nothing else changed).
    /// </summary>
    public void CancelCalibration(CalibrationSnapshot snapshot)
    {
        if (SelectedBody.Surface.Map is not SurfaceMap map)
        {
            return;
        }

        map.Calibration = snapshot.Calibration;
        Surface?.SetCalibration(snapshot.Calibration);
        _gesture = null;  // Nothing to undo: everything is back as it was.
        _editVersion++;
        UpdateUnsavedState();
        Changed?.Invoke();
    }

    /// <summary>
    /// True while the Cut editor is open. Set by the editor, so New/Open can wait until it closes.
    /// </summary>
    public bool IsCutting { get; set; }

    /// <summary>
    /// The selected body's map pieces, bottom to top (VISION.md MAP-02). Read only.
    /// </summary>
    public IReadOnlyList<MapPiece> Pieces => SelectedBody.Surface.Pieces;

    /// <summary>The selected body's map asset name, for cutting pieces from it, or null.</summary>
    public string? MainMapAssetName => SelectedBody.Surface.Map?.AssetName;

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
    /// Cuts a new piece from a source image and lays it on the selected body, on top of the
    /// others.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The body already has the most pieces, or it's a star.
    /// </exception>
    /// <exception cref="MapLoadException">The source image can't be read.</exception>
    public async Task<MapPiece> AddPieceAsync(
        string assetName, PieceOutline outline, GeoCoordinate center, double widthDegrees)
    {
        RequireIdle();
        RequireSurface();
        Body body = SelectedBody;
        if (body.Surface.Pieces.Count >= SurfaceSettings.MaxPieces)
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
            Name = NextPieceName(body),
            AssetName = assetName,
            Outline = outline,
            Center = center,
            WidthDegrees = Math.Clamp(widthDegrees,
                PieceProjection.MinimumWidthDegrees, PieceProjection.MaximumWidthDegrees),
        };
        RecordUndo($"Add {piece.Name}");
        body.Surface.Pieces.Add(piece);
        _pieceTextures[piece.Id] = texture;
        ShowPieces(body);
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

        RecordUndo($"Place {piece.Name}", mergeKey: ("place", id));
        piece.Center = center;
        piece.RotationDegrees = ((rotationDegrees % 360) + 360) % 360;
        piece.WidthDegrees = Math.Clamp(widthDegrees,
            PieceProjection.MinimumWidthDegrees, PieceProjection.MaximumWidthDegrees);
        ShowPieces(SelectedBody);
        MarkChanged();
    }

    /// <summary>Renames a piece. Empty names are ignored.</summary>
    public void RenamePiece(Guid id, string name)
    {
        if (FindPiece(id) is MapPiece piece && !string.IsNullOrWhiteSpace(name)
            && piece.Name != name.Trim())
        {
            RecordUndo($"Rename {piece.Name}");
            piece.Name = name.Trim();
            MarkChanged();
        }
    }

    /// <summary>Removes a piece from the selected body.</summary>
    public void RemovePiece(Guid id)
    {
        if (FindPiece(id) is not MapPiece piece)
        {
            return;
        }

        RecordUndo($"Delete {piece.Name}");
        SelectedBody.Surface.Pieces.Remove(piece);
        ShowPieces(SelectedBody);
        MarkChanged();
    }

    /// <summary>
    /// Moves a piece up (positive) or down (negative) in the layer order; higher pieces cover
    /// lower ones.
    /// </summary>
    public void MovePieceInOrder(Guid id, int steps)
    {
        List<MapPiece> pieces = SelectedBody.Surface.Pieces;
        int index = pieces.FindIndex(p => p.Id == id);
        int target = Math.Clamp(index + steps, 0, pieces.Count - 1);
        if (index < 0 || target == index)
        {
            return;
        }

        MapPiece piece = pieces[index];
        RecordUndo($"Reorder {piece.Name}");
        pieces.RemoveAt(index);
        pieces.Insert(target, piece);
        ShowPieces(SelectedBody);
        MarkChanged();
    }

    /// <summary>
    /// Moves one point of a piece's cut on the globe (Edit Points): the image stretches to
    /// follow. Cheap enough to call while dragging; wrap a drag in a gesture.
    /// </summary>
    /// <param name="id">The piece.</param>
    /// <param name="pointIndex">Which point of its outline.</param>
    /// <param name="position">Where it goes, in the piece's box (may be outside 0–1).</param>
    public void MovePiecePoint(Guid id, int pointIndex, ImagePoint position)
    {
        if (FindPiece(id) is not MapPiece piece
            || pointIndex < 0 || pointIndex >= piece.Outline.Points.Count
            || !double.IsFinite(position.U) || !double.IsFinite(position.V))
        {
            return;
        }

        RecordUndo($"Edit Points of {piece.Name}");
        ImagePoint[] points =
            [.. piece.WarpedPoints ?? PieceWarp.UnwarpedPoints(piece.Outline)];
        points[pointIndex] = position;
        piece.WarpedPoints = points;  // A new list: never edited in place.
        ShowPieces(SelectedBody);
        MarkChanged();
    }

    /// <summary>Removes a piece's warp, back to its cut shape as drawn.</summary>
    public void ResetPiecePoints(Guid id)
    {
        if (FindPiece(id) is not MapPiece { WarpedPoints: not null } piece)
        {
            return;
        }

        RecordUndo($"Reset Points of {piece.Name}");
        piece.WarpedPoints = null;
        ShowPieces(SelectedBody);
        MarkChanged();
    }

    /// <summary>
    /// The baked lookup of a warped piece (null if it isn't warped), for finding it under the
    /// mouse. Kept until the warp changes.
    /// </summary>
    public WarpLookup? WarpLookupFor(MapPiece piece)
    {
        if (piece.WarpedPoints is not IReadOnlyList<ImagePoint> points)
        {
            return null;
        }

        if (_warpLookups.TryGetValue(piece.Id, out var cached)
            && ReferenceEquals(cached.Points, points))
        {
            return cached.Lookup;
        }

        WarpLookup lookup = new PieceWarp(piece.Outline, points).BakeLookup();
        _warpLookups[piece.Id] = (points, lookup);
        return lookup;
    }

    /// <summary>Changes the color used where the selected body's map doesn't cover it.</summary>
    public void SetFillColor(RgbColor color)
    {
        if (color == FillColor)
        {
            return;
        }

        RecordUndo("Change Fill Color", mergeKey: ("fill color", SelectedBodyId));
        SelectedBody.Surface.FillColor = color;
        ShowSurfaceSettings(SelectedBody);
        MarkChanged();
    }

    /// <summary>
    /// True if Undo is possible now: there's a step to undo, and nothing is in progress (a save,
    /// open, drag, calibration, or cut).
    /// </summary>
    public bool CanUndo => _history.CanUndo && IsIdleForHistory;

    /// <summary>True if Redo is possible now (see <see cref="CanUndo"/>).</summary>
    public bool CanRedo => _history.CanRedo && IsIdleForHistory;

    /// <summary>What Undo would undo, e.g. "Move Piece 1", or null.</summary>
    public string? UndoDescription => _history.UndoDescription;

    /// <summary>What Redo would redo, or null.</summary>
    public string? RedoDescription => _history.RedoDescription;

    /// <summary>
    /// Takes back the last edit, selecting the body it changed so the change is visible. If
    /// that brings back a different map image, the image is reloaded (a few seconds for a
    /// large map).
    /// </summary>
    /// <returns>What was undone and, if an image couldn't be shown, a warning.</returns>
    /// <exception cref="InvalidOperationException">Undo isn't possible now.</exception>
    public Task<(string Description, string? Warning)> UndoAsync()
    {
        if (!CanUndo)
        {
            throw new InvalidOperationException("There's nothing to undo right now.");
        }

        string description = _history.UndoDescription!;
        return RestoreAsync(_history.Undo(CurrentSurfaceOfSameBody), description);
    }

    /// <summary>Re-applies the last undone edit (see <see cref="UndoAsync"/>).</summary>
    /// <exception cref="InvalidOperationException">Redo isn't possible now.</exception>
    public Task<(string Description, string? Warning)> RedoAsync()
    {
        if (!CanRedo)
        {
            throw new InvalidOperationException("There's nothing to redo right now.");
        }

        string description = _history.RedoDescription!;
        return RestoreAsync(_history.Redo(CurrentSurfaceOfSameBody), description);
    }

    /// <summary>
    /// Starts a gesture, such as dragging a piece: its edits (however many) become one undo
    /// step, recorded by <see cref="EndGesture"/>, and only if anything changed.
    /// </summary>
    /// <param name="description">What the gesture does, e.g. "Move Piece 1".</param>
    public void BeginGesture(string description)
    {
        EndGesture();
        _gesture = (description, SnapshotSelected(), _editVersion);
        Changed?.Invoke();  // Undo isn't available mid-gesture.
    }

    /// <summary>Ends the gesture started by <see cref="BeginGesture"/>.</summary>
    public void EndGesture()
    {
        if (_gesture is not (string description, SurfaceSnapshot before, int version))
        {
            return;
        }

        _gesture = null;
        if (version != _editVersion)
        {
            _history.Record(description, before);
            ForgetUnusedTextures();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Call when the app is quitting, after the user has saved or chosen to discard.
    /// </summary>
    public void Close()
    {
        CloseCurrentWorld();
        ResetHistory();
    }

    private async Task<string?> LoadAndShowAsync(string readPath, string? savedPath, bool unsaved)
    {
        RequireIdle();
        SetBusy(true);
        LoadedWorld loaded;
        LoadedMap? map = null;
        Guid selected;
        string? warning = null;
        Dictionary<Guid, Texture2D> pieceTextures;
        try
        {
            loaded = await Task.Run(() => WorldPackage.Load(readPath));
            selected = DefaultSelection(loaded.World);

            // If the map image can't be shown, still open the world and keep the image data,
            // so saving never loses it.
            Body selectedBody = loaded.World.Bodies.First(body => body.Id == selected);
            if (selectedBody.Surface.Map is SurfaceMap surfaceMap)
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
                [.. loaded.World.Bodies.SelectMany(body => body.Surface.Pieces)],
                loaded.Assets);
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
        ResetHistory();
        ForgetMaps();
        _pieceTextures = pieceTextures;
        World = loaded.World;
        _assets = new Dictionary<string, IAssetSource>(loaded.Assets);
        FilePath = savedPath;
        HasUnsavedChanges = unsaved;
        SelectedBodyId = selected;
        _savedSurfaces = unsaved ? null : SnapshotSurfaces(World);
        _projectionWithoutMap = MapProjection.Mercator;
        ShowWorld();
        if (map is not null && SelectedBody.Surface.Map is SurfaceMap shown)
        {
            Surface?.SetMap(map.Texture);
            _mapChecks[selected] = map.Check;
            _fullMap = (selected, shown.AssetName);
        }

        Camera?.SetView(World.View);
        _editVersion++;
        SelectionChanged?.Invoke();
        TimeChanged?.Invoke();
        Changed?.Invoke();
        _ = LoadPreviewsAsync(World.Id);
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

    // Shows a body's map at full size (or clears it if it has none), unless it already is.
    private async Task<string?> ShowFullMapAsync(Body body)
    {
        PlanetSurface? surface = System?.SurfaceFor(body.Id);
        if (body.Surface.Map is not SurfaceMap map)
        {
            surface?.ClearMap();
            _mapChecks.Remove(body.Id);
            return null;
        }

        if (_fullMap == (body.Id, map.AssetName))
        {
            return null;
        }

        SetBusy(true);
        try
        {
            LoadedMap loaded =
                await MapImageLoader.LoadAsync(_assets[map.AssetName], map.AssetName);
            surface?.SetMap(loaded.Texture);
            _mapChecks[body.Id] = loaded.Check;
            _fullMap = (body.Id, map.AssetName);
            return null;
        }
        catch (MapLoadException error)
        {
            // The map stays in the world data, so saving keeps it.
            surface?.ClearMap();
            _mapChecks.Remove(body.Id);
            return $"The map image couldn't be shown: {error.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    // A body that's no longer selected swaps its full-size map for the small preview, freeing
    // graphics memory. If the preview isn't ready yet, it's loaded first.
    private void ShowPreviewInsteadOfFullMap(Body body)
    {
        if (_fullMap?.Body != body.Id || body.Surface.Map is not SurfaceMap map)
        {
            return;
        }

        if (_previews.TryGetValue(body.Id, out var preview) && preview.Asset == map.AssetName)
        {
            System?.SurfaceFor(body.Id)?.SetMap(preview.Texture);
            _fullMap = null;
        }
        else
        {
            _ = LoadPreviewAsync(body, World.Id);
        }
    }

    // Loads previews for every mapped body in the background after a world opens, one at a
    // time so the app stays responsive.
    private async Task LoadPreviewsAsync(Guid worldId)
    {
        foreach (Body body in World.Bodies.ToList())
        {
            if (World.Id != worldId)
            {
                return;  // Another world was opened meanwhile.
            }

            await LoadPreviewAsync(body, worldId);
        }
    }

    // Loads one body's map preview, and shows it if the body isn't the selected one.
    private async Task LoadPreviewAsync(Body body, Guid worldId)
    {
        if (body.Surface.Map is not SurfaceMap map
            || _assets.GetValueOrDefault(map.AssetName) is not IAssetSource source)
        {
            return;
        }

        if (!_previews.TryGetValue(body.Id, out var preview) || preview.Asset != map.AssetName)
        {
            try
            {
                Texture2D texture = await MapImageLoader.LoadPreviewAsync(source, map.AssetName);
                preview = (map.AssetName, texture);
            }
            catch (MapLoadException error)
            {
                GD.PushWarning($"Couldn't load a preview of {body.Name}'s map: {error.Message}");
                return;
            }

            if (World.Id != worldId)
            {
                return;
            }

            _previews[body.Id] = preview;
        }

        if (body.Id != SelectedBodyId && body.Surface.Map?.AssetName == preview.Asset)
        {
            System?.SurfaceFor(body.Id)?.SetMap(preview.Texture);
            if (_fullMap?.Body == body.Id)
            {
                _fullMap = null;
            }
        }
    }

    private void ForgetMaps()
    {
        _mapChecks.Clear();
        _previews.Clear();
        _fullMap = null;
    }

    // Builds the system view for the open world, and shows every body's surface settings and
    // pieces (maps are loaded separately).
    private void ShowWorld()
    {
        System?.Show(World, SelectedBodyId);
        foreach (Body body in World.Bodies)
        {
            ShowSurfaceSettings(body);
            ShowPieces(body);
        }
    }

    private void ShowSurfaceSettings(Body body)
    {
        if (System?.SurfaceFor(body.Id) is not PlanetSurface surface)
        {
            return;
        }

        surface.Projection = body.Surface.Map?.Projection
            ?? (body.Id == SelectedBodyId ? _projectionWithoutMap : MapProjection.Mercator);
        surface.FillColor = body.Surface.FillColor.ToGodot();
        surface.SetCalibration(body.Surface.Map?.Calibration);
    }

    // Sends every piece of a body that has a texture to its surface, bottom to top.
    private void ShowPieces(Body body)
    {
        var allPieces = new HashSet<Guid>(World.Bodies
            .SelectMany(b => b.Surface.Pieces.Select(piece => piece.Id)));
        foreach (Guid id in _warpLookups.Keys.Where(id => !allPieces.Contains(id)).ToList())
        {
            _warpLookups.Remove(id);
        }

        System?.SurfaceFor(body.Id)?.SetPieces([.. body.Surface.Pieces
            .Where(p => _pieceTextures.ContainsKey(p.Id))
            .Select(p => (_pieceTextures[p.Id], PieceProjection.For(p), WarpLookupFor(p)))]);
    }

    private Body? FindBody(Guid id) => World.Bodies.Find(body => body.Id == id);

    private MapPiece? FindPiece(Guid id) => SelectedBody.Surface.Pieces.Find(p => p.Id == id);

    private static string NextPieceName(Body body)
    {
        int number = 1;
        while (body.Surface.Pieces.Any(p => p.Name == $"Piece {number}"))
        {
            number++;
        }

        return $"Piece {number}";
    }

    // The body selected when a world opens: the first planet or moon, so the map tools are
    // ready to use; a star only if there's nothing else.
    private static Guid DefaultSelection(World world)
    {
        return (world.Bodies.FirstOrDefault(body => body.Kind != BodyKind.Star)
            ?? world.Bodies[0]).Id;
    }

    private static Dictionary<Guid, SurfaceSettings> SnapshotSurfaces(World world)
    {
        return world.Bodies.ToDictionary(body => body.Id, body => body.Surface.Clone());
    }

    private bool IsIdleForHistory =>
        !IsBusy && _gesture is null && !IsCalibrating && !IsCutting;

    private SurfaceSnapshot SnapshotSelected()
    {
        return new SurfaceSnapshot(SelectedBodyId, SelectedBody.Surface.Clone());
    }

    // For undo and redo: the current surface of the body a step belongs to.
    private SurfaceSnapshot CurrentSurfaceOfSameBody(SurfaceSnapshot step)
    {
        return new SurfaceSnapshot(
            step.BodyId, (FindBody(step.BodyId)?.Surface ?? step.Surface).Clone());
    }

    // Call just before changing the surface. Inside a gesture, the gesture records it instead.
    private void RecordUndo(string description, object? mergeKey = null)
    {
        if (_gesture is null)
        {
            _history.Record(description, SnapshotSelected(), mergeKey);
            ForgetUnusedTextures();
        }
    }

    // Puts back a snapshot from the history and selects its body, reloading the map image if
    // it's a different one.
    private async Task<(string Description, string? Warning)> RestoreAsync(
        SurfaceSnapshot snapshot, string description)
    {
        if (FindBody(snapshot.BodyId) is not Body body)
        {
            return (description, "The body that edit belonged to no longer exists.");
        }

        body.Surface.RestoreFrom(snapshot.Surface);
        ShowSurfaceSettings(body);
        ShowPieces(body);
        string? warning = body.Id == SelectedBodyId
            ? await ShowFullMapAsync(body)
            : await SelectBodyAsync(body.Id);
        ForgetUnusedTextures();
        MarkChanged();
        return (description, warning);
    }

    // Piece textures are kept while the world or its undo history still has the piece, so
    // undoing a delete is instant. The rest are freed.
    private void ForgetUnusedTextures()
    {
        var used = new HashSet<Guid>(_history.States.Select(state => state.Surface)
            .Concat(World.Bodies.Select(body => body.Surface))
            .SelectMany(surface => surface.Pieces.Select(piece => piece.Id)));
        foreach (Guid id in _pieceTextures.Keys.Where(id => !used.Contains(id)).ToList())
        {
            _pieceTextures.Remove(id);
        }
    }

    // Images the undo history needs but the world being saved doesn't, which are read from the
    // file about to be replaced. They must be copied out first, or undo couldn't bring them back.
    private List<string> AssetsOnlyInHistory(World saving, string savePath)
    {
        var saved = new HashSet<string>(WorldPackage.ReferencedAssetNames(saving));
        return [.. _history.States
            .Select(state => state.Surface)
            .SelectMany(surface => surface.Pieces.Select(piece => piece.AssetName)
                .Append(surface.Map?.AssetName))
            .OfType<string>()
            .Distinct()
            .Where(name => !saved.Contains(name)
                && _assets.GetValueOrDefault(name) is PackageAssetSource source
                && string.Equals(source.PackagePath, savePath,
                    StringComparison.OrdinalIgnoreCase))];
    }

    private AssetStash Stash()
    {
        return _stash ??= new AssetStash(Path.Combine(
            Path.GetTempPath(), "NothicWorlds", $"undo-{Guid.NewGuid():N}"));
    }

    private void ResetHistory()
    {
        _history.Clear();
        _gesture = null;
        _stash?.Dispose();
        _stash = null;
    }

    private void MarkChanged()
    {
        _editVersion++;
        UpdateUnsavedState();
        Changed?.Invoke();
    }

    private void UpdateUnsavedState()
    {
        HasUnsavedChanges = _savedSurfaces is null
            || _savedSurfaces.Count != World.Bodies.Count
            || World.Bodies.Any(body =>
                !_savedSurfaces.TryGetValue(body.Id, out SurfaceSettings? saved)
                || !body.Surface.HasSameContent(saved));
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

    private void RequireSurface()
    {
        if (!SelectedBodyHasSurface)
        {
            throw new InvalidOperationException(
                "Stars don't have maps. Select a planet or moon first.");
        }
    }

    // One body's surface at one moment, for undo.
    private sealed record SurfaceSnapshot(Guid BodyId, SurfaceSettings Surface);
}
