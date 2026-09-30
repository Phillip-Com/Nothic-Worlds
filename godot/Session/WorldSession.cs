using Godot;
using NothicWorlds.Controls;
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
        Surface?.ClearMap();
        ShowSurfaceSettings();
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
        // original image files (which the user may move or delete).
        foreach (string name in assets.Keys.Where(_assets.ContainsKey).ToList())
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
        }
        finally
        {
            SetBusy(false);
        }

        CloseCurrentWorld();
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
        Camera?.SetView(World.View);
        _editVersion++;
        Changed?.Invoke();
        return warning;
    }

    private void ShowSurfaceSettings()
    {
        if (Surface is null)
        {
            return;
        }

        Surface.Projection = Projection;
        Surface.FillColor = FillColor.ToGodot();
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
