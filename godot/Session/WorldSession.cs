using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Editing;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
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
/// calibrate, pieces) and the System panel's properties work on it, and the view flies to it.
/// Only the selected body's map is kept at full size; the others show a small preview, so a
/// system of mapped planets fits the baseline laptop's graphics memory.</para>
/// <para>Moving the camera, or the world clock, doesn't count as an unsaved change (it would
/// make the warning appear constantly), but both are saved with the world.</para>
/// <para>Every edit can be undone: each one records a copy of the bodies first (see
/// <see cref="UndoAsync"/>).</para>
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

    // Maps: what each body's globe shows now (which image, and whether at full size), the one
    // full-size map kept (for the selected body), and small previews by image.
    private readonly Dictionary<Guid, (string Asset, bool Full)> _shownMaps = [];
    private (string Asset, Texture2D Texture, MapImageCheck Check)? _fullMap;
    private readonly Dictionary<string, Texture2D> _previews = [];
    private readonly HashSet<string> _previewsLoading = [];

    // The map type picked while the selected body has no map; its next import uses it.
    private MapProjection _projectionWithoutMap = MapProjection.Mercator;

    // Increases with every edit, so a gesture can tell whether it changed anything.
    private int _editVersion;

    // Increases with every edit that could change the simulation (not journal writing), so the
    // seasons and eclipses are only worked out again when they might have changed.
    private int _systemVersion;

    // The selected body's seasons, and which world, edit, and body they were worked out for.
    private (World World, int Version, Guid BodyId, SeasonTimeline Timeline)? _seasons;

    // The selected body's eclipses, worked out in the background (see SelectedEclipses): the
    // latest result, and what's being worked out now.
    private (World World, int Version, Guid BodyId, EclipseTimeline Timeline)? _eclipses;
    private (World World, int Version, Guid BodyId)? _eclipsesUnderway;
    private (World World, int Version, Guid BodyId)? _eclipsesFailed;

    // The selected body's meteor showers, worked out the same way (see SelectedMeteorShowers).
    private (World World, int Version, Guid BodyId, MeteorShowerTimeline Timeline)? _showers;
    private (World World, int Version, Guid BodyId)? _showersUnderway;
    private (World World, int Version, Guid BodyId)? _showersFailed;

    // Undo/redo: snapshots of every body. While a gesture (a drag, or a calibration session) is
    // under way, its edits add up to one step, recorded when it ends.
    private readonly UndoHistory<EditSnapshot> _history = new();
    private (string Description, EditSnapshot Before, int Version)? _gesture;

    // Copies of images only the undo history still needs (see AssetStash).
    private AssetStash? _stash;

    // Every body as it is in the saved file (or as a new world started), so the world counts as
    // saved whenever it matches again, e.g. after undoing back to it. Null when there's nothing
    // to match (a recovered world is unsaved until it's saved).
    private List<Body>? _savedBodies;
    private LoreState? _savedLore;

    /// <summary>Raised when anything shown about the world changes (name, file, unsaved state,
    /// busy state, selection, bodies, or contents).</summary>
    public event Action? Changed;

    /// <summary>Raised when a different body is selected (or a world opens).</summary>
    public event Action? SelectionChanged;

    /// <summary>Raised when the world clock changes (often: every frame while it runs).</summary>
    public event Action? TimeChanged;

    /// <summary>
    /// Raised when the selected body's eclipses have been worked out (see
    /// <see cref="SelectedEclipses"/>).
    /// </summary>
    public event Action? EclipsesReady;

    /// <summary>
    /// Raised when the selected body's meteor showers have been worked out (see
    /// <see cref="SelectedMeteorShowers"/>).
    /// </summary>
    public event Action? MeteorShowersReady;

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

    /// <summary>The selected body: the one the tools work on and the view centers on.</summary>
    public Body SelectedBody => FindBody(SelectedBodyId) ?? World.Bodies[0];

    /// <summary>True if the selected body can have a map (planets and moons; not stars).</summary>
    public bool SelectedBodyHasSurface => SelectedBody.HasSurface;

    /// <summary>The selected body's surface, or null for a star.</summary>
    public PlanetSurface? Surface => System?.SurfaceFor(SelectedBodyId);

    /// <summary>
    /// The size check of the selected body's map, or null if it has none (or it's still
    /// loading).
    /// </summary>
    public MapImageCheck? MapCheck =>
        SelectedBody.Surface.Map?.AssetName is string asset && _fullMap?.Asset == asset
            ? _fullMap.Value.Check
            : null;

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

    /// <summary>
    /// Physics mode (VISION.md SIM-03): the system view following real gravity, while it's on.
    /// </summary>
    public PhysicsMode Physics { get; } = new();

    public override void _Ready()
    {
        SelectedBodyId = DefaultSelection(World);
        _savedBodies = CloneBodies(World.Bodies);
        _savedLore = LoreState.Of(World);
        Changed += () => Physics.RestartIfRedesigned(World.Bodies, TimeDays);
        ShowWorld();
    }

    public override void _ExitTree()
    {
        // However the app ends, don't leave the undo copies in the temporary folder.
        _stash?.Dispose();
        _stash = null;
        Physics.Dispose();
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
        _savedBodies = CloneBodies(World.Bodies);
        _savedLore = LoreState.Of(World);
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
        _savedBodies = snapshot.Bodies;  // A private copy, made before saving.
        _savedLore = LoreState.Of(snapshot);
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
    /// Selects a body: the view flies to it, and the tools work on it from now on. Its map is
    /// loaded at full size (the previously selected body goes back to a preview). Does nothing
    /// while something else is in progress.
    /// </summary>
    /// <returns>A warning if the body's map couldn't be shown; otherwise null.</returns>
    public async Task<string?> SelectBodyAsync(Guid bodyId)
    {
        if (bodyId == SelectedBodyId || FindBody(bodyId) is null || !IsIdleForHistory)
        {
            return null;
        }

        SelectedBodyId = bodyId;
        System?.FlyTo(bodyId);
        SelectionChanged?.Invoke();
        Changed?.Invoke();
        return await ShowMapsAsync();
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

    // ----- Bodies (System panel) -----

    /// <summary>
    /// Adds a body with sensible starting values (VISION.md BOD-01) and selects it:
    /// <list type="bullet">
    /// <item>A planet orbits the selected star, or the star the selected body belongs to.</item>
    /// <item>A moon orbits the selected planet or moon (not a star).</item>
    /// <item>A star becomes a far-out companion of the system's central star.</item>
    /// <item>A comet circles the selected body's star (or the first star), crossing the
    /// innermost planet's orbit.</item>
    /// </list>
    /// </summary>
    /// <returns>
    /// The new body, or null if it can't be added here (a moon of a star or comet, or a comet
    /// with no star in the system).
    /// </returns>
    public async Task<Body?> AddBodyAsync(BodyKind kind)
    {
        if (!IsIdleForHistory)
        {
            return null;
        }

        Body selected = SelectedBody;
        Body? body = kind switch
        {
            BodyKind.Planet => NewBodies.Planet(World.Bodies, StarOf(selected) ?? Root(selected)),
            BodyKind.Moon when selected.HasSurface => NewBodies.Moon(World.Bodies, selected),
            BodyKind.Star => NewBodies.Star(World.Bodies, Root(selected)),
            BodyKind.WorldTree =>
                NewBodies.WorldTree(World.Bodies, StarOf(selected) ?? Root(selected)),
            BodyKind.Comet when (StarOf(selected) ?? World.Bodies.Find(IsStar)) is Body star =>
                NewBodies.Comet(World.Bodies, star),
            _ => null,
        };
        if (body is null)
        {
            return null;
        }

        RecordUndo($"Add {body.Name}");
        World.Bodies.Add(body);
        SyncView();
        MarkChanged();
        await SelectBodyAsync(body.Id);
        return body;
    }

    /// <summary>
    /// Removes a body and everything orbiting it (owner decision; Ctrl+Z brings them back).
    /// The last body can't be removed.
    /// </summary>
    /// <returns>What was removed (e.g. "Planet and 2 bodies orbiting it"), or null.</returns>
    public async Task<string?> RemoveBodyAsync(Guid bodyId)
    {
        if (!IsIdleForHistory || FindBody(bodyId) is not Body body)
        {
            return null;
        }

        List<Body> descendants = SystemHierarchy.DescendantsOf(World.Bodies, bodyId);
        if (descendants.Count + 1 >= World.Bodies.Count)
        {
            return null;
        }

        string what = descendants.Count == 0 ? body.Name
            : $"{body.Name} and {descendants.Count} bod{(descendants.Count == 1 ? "y" : "ies")} " +
              "orbiting it";
        RecordUndo($"Delete {body.Name}");
        var removed = new HashSet<Guid>(descendants.Select(d => d.Id)) { bodyId };
        Guid? parent = body.Orbit?.ParentId;
        World.Bodies.RemoveAll(b => removed.Contains(b.Id));
        ClearLoreOn(removed);
        foreach (Body other in World.Bodies.Where(b =>
            b.Calendar?.MonthMoonId is Guid moon && removed.Contains(moon)))
        {
            other.Calendar = other.Calendar! with { MonthMoonId = null };
        }

        if (removed.Contains(SelectedBodyId))
        {
            SelectedBodyId = parent is Guid p && FindBody(p) is not null
                ? p
                : DefaultSelection(World);
            SelectionChanged?.Invoke();
        }

        SyncView();
        System?.FlyTo(SelectedBodyId);
        MarkChanged();
        await ShowMapsAsync();
        return what;
    }

    /// <summary>Renames a body. Empty names are ignored.</summary>
    public void RenameBody(Guid bodyId, string name)
    {
        if (FindBody(bodyId) is Body body && !string.IsNullOrWhiteSpace(name)
            && body.Name != name.Trim())
        {
            RecordUndo($"Rename {body.Name}");
            body.Name = name.Trim();
            MarkChanged();
            TimeChanged?.Invoke();  // The time bar shows the name.
        }
    }

    /// <summary>Adds a nebula to the sky (VISION.md BOD-03).</summary>
    /// <returns>The new nebula, or null if the sky already has as many as it can.</returns>
    public Nebula? AddNebula()
    {
        if (World.Nebulas.Count >= Nebula.MaxCount)
        {
            return null;
        }

        Nebula nebula = NewBodies.Nebula(World.Nebulas);
        RecordUndo($"Add {nebula.Name}");
        World.Nebulas.Add(nebula);
        MarkChanged(systemChanged: false);
        return nebula;
    }

    /// <summary>
    /// Changes a nebula (matched by ID). Rapid changes (dragging a field or a color) are one
    /// undo step.
    /// </summary>
    /// <returns>What's wrong with the nebula (nothing is changed then), or null.</returns>
    public string? SetNebula(Nebula nebula)
    {
        int index = World.Nebulas.FindIndex(n => n.Id == nebula.Id);
        if (index < 0 || World.Nebulas[index] == nebula)
        {
            return null;
        }

        if (nebula.Problem() is string problem)
        {
            return problem;
        }

        RecordUndo($"Edit {World.Nebulas[index].Name}", mergeKey: ("nebula", nebula.Id));
        World.Nebulas[index] = nebula;
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>Removes a nebula from the sky.</summary>
    public void RemoveNebula(Guid nebulaId)
    {
        if (World.Nebulas.Find(n => n.Id == nebulaId) is not Nebula nebula)
        {
            return;
        }

        RecordUndo($"Delete {nebula.Name}");
        World.Nebulas.Remove(nebula);
        MarkChanged(systemChanged: false);
    }

    /// <summary>
    /// Adds an asteroid belt to a star (VISION.md BOD-03), like our main belt or just beyond
    /// its outermost belt.
    /// </summary>
    /// <returns>The new belt, or null if the body isn't a star.</returns>
    public AsteroidBelt? AddBelt(Guid starId)
    {
        if (FindBody(starId) is not { Kind: BodyKind.Star } star)
        {
            return null;
        }

        AsteroidBelt belt = NewBodies.Belt(star);
        RecordUndo($"Add {belt.Name} to {star.Name}");
        star.Belts = [.. star.Belts, belt];
        MarkChanged(systemChanged: false);
        return belt;
    }

    /// <summary>
    /// Changes one of a star's belts (matched by ID). Rapid changes (dragging a field or a
    /// color) are one undo step.
    /// </summary>
    /// <returns>What's wrong with the belt (nothing is changed then), or null.</returns>
    public string? SetBelt(Guid starId, AsteroidBelt belt)
    {
        if (FindBody(starId) is not Body star
            || star.Belts.FirstOrDefault(b => b.Id == belt.Id) is not AsteroidBelt before
            || before == belt)
        {
            return null;
        }

        if (belt.Problem() is string problem)
        {
            return problem;
        }

        RecordUndo($"Edit {before.Name}", mergeKey: ("belt", belt.Id));
        star.Belts = [.. star.Belts.Select(b => b.Id == belt.Id ? belt : b)];
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>Removes one of a star's belts.</summary>
    public void RemoveBelt(Guid starId, Guid beltId)
    {
        if (FindBody(starId) is not Body star
            || star.Belts.FirstOrDefault(b => b.Id == beltId) is not AsteroidBelt belt)
        {
            return;
        }

        RecordUndo($"Delete {belt.Name}");
        star.Belts = [.. star.Belts.Where(b => b.Id != beltId)];
        MarkChanged(systemChanged: false);
    }

    /// <summary>
    /// Hangs a planet or moon on a world tree's branch as a realm (VISION.md BOD-02), or lets it
    /// go with a null tree: it then keeps circling the tree where it was, now freely.
    /// </summary>
    /// <returns>What's wrong (nothing is changed then), or null.</returns>
    public string? HangOnBranch(Guid bodyId, Guid? treeId, int branch)
    {
        if (FindBody(bodyId) is not Body body || !body.HasSurface)
        {
            return null;
        }

        if (treeId is not Guid id)
        {
            if (body.Branch is null)
            {
                return null;
            }

            RecordUndo($"Let {body.Name} Go");
            body.Branch = null;
            SyncView();
            MarkChanged();
            return null;
        }

        if (FindBody(id) is not { Tree: WorldTreeLook } tree)
        {
            return "only a world tree has branches";
        }

        if (body.Branch == branch && body.Orbit?.ParentId == id)
        {
            return null;
        }

        // Check before changing anything (the tree mustn't hang on its own realm, either).
        (int? oldBranch, Orbit? oldOrbit) = (body.Branch, body.Orbit);
        body.Branch = branch;
        body.Orbit = Realms.OrbitOnBranch(tree, branch);
        string? problem = Realms.Problem(World.Bodies) ?? SystemHierarchy.Problem(World.Bodies);
        (body.Branch, body.Orbit) = (oldBranch, oldOrbit);
        if (problem is not null)
        {
            return problem;
        }

        RecordUndo($"Hang {body.Name} on {tree.Name}");
        body.Branch = branch;
        body.Orbit = Realms.OrbitOnBranch(tree, branch);
        SyncView();
        MarkChanged();
        return null;
    }

    /// <summary>
    /// Changes how a world tree grows and looks (VISION.md BOD-02). Rapid changes (dragging a
    /// field or a color) are one undo step.
    /// </summary>
    /// <returns>What's wrong with the look (nothing is changed then), or null.</returns>
    public string? SetTreeLook(Guid treeId, WorldTreeLook look)
    {
        if (FindBody(treeId) is not { Kind: BodyKind.WorldTree } tree || tree.Tree == look)
        {
            return null;
        }

        if (look.Problem() is string problem)
        {
            return problem;
        }

        // A branch holding a realm can't be taken away.
        int? highest = World.Bodies
            .Where(b => b.Branch is not null && b.Orbit?.ParentId == treeId)
            .Max(b => b.Branch);
        if (highest is int used && used >= look.Branches)
        {
            return $"a realm hangs on branch {used + 1}: move it first";
        }

        RecordUndo($"Change {tree.Name}'s Look", mergeKey: ("tree", treeId));
        tree.Tree = look;
        MarkChanged();  // Its realms move with its branches.
        return null;
    }

    /// <summary>
    /// Gives a planet or moon rings (VISION.md BOD-03), changes them, or removes them with
    /// null. Rapid changes (dragging a field or a color) are one undo step.
    /// </summary>
    /// <returns>What's wrong with the rings (nothing is changed then), or null.</returns>
    public string? SetRings(Guid bodyId, PlanetRings? rings)
    {
        if (FindBody(bodyId) is not Body body || body.Rings == rings || !body.HasSurface)
        {
            return null;
        }

        if (rings?.Problem() is string problem)
        {
            return problem;
        }

        RecordUndo(rings is null ? $"Remove {body.Name}'s Rings"
            : body.Rings is null ? $"Give {body.Name} Rings"
            : $"Edit {body.Name}'s Rings", mergeKey: ("rings", bodyId));
        body.Rings = rings;
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>
    /// Makes a planet or moon a globe or a flat world (VISION.md BOD-02). Its maps, terrain,
    /// regions, and pins stay where they are on the map; only the body's shape changes.
    /// </summary>
    public void SetBodyShape(Guid bodyId, BodyShape shape)
    {
        if (FindBody(bodyId) is not Body body || body.Shape == shape || !body.HasSurface)
        {
            return;
        }

        RecordUndo(shape == BodyShape.FlatDisc
            ? $"Make {body.Name} a Flat World"
            : $"Make {body.Name} a Globe");
        body.Shape = shape;
        SyncView();
        MarkChanged();
    }

    /// <summary>
    /// Switches a body between planet and moon (stars and comets stay as they are).
    /// </summary>
    public void SetBodyKind(Guid bodyId, BodyKind kind)
    {
        if (FindBody(bodyId) is not Body body || body.Kind == kind
            || !body.HasSurface || kind is not (BodyKind.Planet or BodyKind.Moon))
        {
            return;
        }

        RecordUndo($"Change {body.Name} to a {(kind == BodyKind.Moon ? "Moon" : "Planet")}");
        body.Kind = kind;
        MarkChanged();
    }

    /// <summary>
    /// Changes a body's size, day length, and axial tilt (how far its axis leans, and which
    /// way). Rapid changes (holding a field's arrow) are one undo step.
    /// </summary>
    /// <returns>What's wrong with the values (nothing is changed then), or null.</returns>
    public string? SetBodyPhysical(
        Guid bodyId, double radiusKm, double dayHours, double tilt, double tiltDirection)
    {
        if (FindBody(bodyId) is not Body body)
        {
            return null;
        }

        var check = new Body
        {
            RadiusKm = radiusKm,
            DayLengthHours = dayHours,
            AxialTiltDegrees = tilt,
            AxialTiltDirectionDegrees = tiltDirection,
        };
        if (check.Problem() is string problem)
        {
            return problem;
        }

        if (body.RadiusKm == radiusKm && body.DayLengthHours == dayHours
            && body.AxialTiltDegrees == tilt && body.AxialTiltDirectionDegrees == tiltDirection)
        {
            return null;
        }

        RecordUndo($"Edit {body.Name}", mergeKey: ("physical", bodyId));
        body.RadiusKm = radiusKm;
        body.DayLengthHours = dayHours;
        body.AxialTiltDegrees = tilt;
        body.AxialTiltDirectionDegrees = tiltDirection;
        SyncView();
        MarkChanged();
        TimeChanged?.Invoke();  // The date shown depends on the day length.
        return null;
    }

    /// <summary>
    /// Keeps the paths physics mode has the bodies on now as their designed orbits (VISION.md
    /// SIM-03; owner's choice), as one undo step, and switches physics off: the view carries
    /// on from the same places. Bodies that merged away or are escaping keep their designs.
    /// </summary>
    /// <returns>
    /// Why each body that couldn't keep its path didn't, by body; null if physics is off.
    /// </returns>
    public IReadOnlyDictionary<Guid, string>? KeepPhysicsOrbits()
    {
        if (Physics.Keep(TimeDays) is not KeptOrbits kept)
        {
            return null;
        }

        Physics.Stop();
        if (kept.Orbits.Count > 0)
        {
            RecordUndo("Keep physics orbits");
            foreach ((Guid bodyId, Orbit orbit) in kept.Orbits)
            {
                if (FindBody(bodyId) is Body body)
                {
                    body.Orbit = orbit;
                }
            }

            SyncView();
            MarkChanged();
        }

        return kept.NotKept;
    }

    /// <summary>
    /// Sets how dense a body is (VISION.md SIM-04), or null for the typical density of its kind
    /// and size. Rapid changes (holding the field's arrow) are one undo step.
    /// </summary>
    /// <returns>What's wrong with the value (nothing is changed then), or null.</returns>
    public string? SetDensity(Guid bodyId, double? densityGramsPerCm3)
    {
        if (FindBody(bodyId) is not Body body || body.DensityGramsPerCm3 == densityGramsPerCm3)
        {
            return null;
        }

        if (new Body { DensityGramsPerCm3 = densityGramsPerCm3 }.Problem() is string problem)
        {
            return problem;
        }

        // Going back to typical is its own step, not merged into typing a value.
        RecordUndo($"Edit {body.Name}",
            mergeKey: densityGramsPerCm3 is null ? null : ("density", bodyId));
        body.DensityGramsPerCm3 = densityGramsPerCm3;
        MarkChanged();
        return null;
    }

    /// <summary>
    /// Replaces a body's orbit (its parent, size, period, and extras). Rapid changes are one
    /// undo step.
    /// </summary>
    /// <returns>What's wrong with the orbit (nothing is changed then), or null.</returns>
    public string? SetOrbit(Guid bodyId, Orbit orbit)
    {
        if (FindBody(bodyId) is not Body body || body.Orbit == orbit)
        {
            return null;
        }

        if (body.Branch is not null)
        {
            return $"{body.Name} hangs on a branch, which sets its orbit";
        }

        if (orbit.Problem() is string problem)
        {
            return problem;
        }

        // Check the new parent wouldn't make a loop before changing anything.
        Orbit? before = body.Orbit;
        body.Orbit = orbit;
        string? loop = SystemHierarchy.Problem(World.Bodies);
        body.Orbit = before;
        if (loop is not null)
        {
            return loop;
        }

        RecordUndo($"Edit {body.Name}'s Orbit", mergeKey: ("orbit", bodyId));
        body.Orbit = orbit;
        SyncView();
        MarkChanged();
        return null;
    }

    /// <summary>
    /// Makes a body the center of its system (owner decision: "swap places, keep motion"): it
    /// stops orbiting, and the bodies it orbited flip to circle it on the same paths, so every
    /// body stays in the same place relative to the others.
    /// </summary>
    /// <returns>True if anything changed (false if the body was already the center).</returns>
    public bool MakeCenter(Guid bodyId)
    {
        if (FindBody(bodyId) is not Body body)
        {
            return false;
        }

        Dictionary<Guid, Orbit?> changes = SystemHierarchy.MakeCenter(World.Bodies, bodyId);
        if (changes.Count == 0)
        {
            return false;
        }

        RecordUndo($"Make {body.Name} the Center");
        foreach ((Guid id, Orbit? orbit) in changes)
        {
            FindBody(id)!.Orbit = orbit;
        }

        SyncView();
        MarkChanged();
        return true;
    }

    /// <summary>
    /// Changes how a body looks (VISION.md BOD-06): a planet's or moon's color and pattern, or
    /// a star's type. Rapid changes (dragging through colors) are one undo step.
    /// </summary>
    public void SetAppearance(Guid bodyId, BodyAppearance appearance)
    {
        if (FindBody(bodyId) is not Body body || body.Appearance == appearance)
        {
            return;
        }

        RecordUndo($"Change {body.Name}'s Look", mergeKey: ("appearance", bodyId));
        body.Appearance = appearance;
        ShowSurfaceSettings(body);
        MarkChanged(systemChanged: false);
    }

    /// <summary>
    /// Replaces a body's calendar (VISION.md CAL-01), or removes it with null. Stars don't
    /// have calendars. A calendar that fits the world (CAL-02) adjusts it straight away, in the
    /// same undo step, and keeps it fitted after later edits.
    /// </summary>
    /// <returns>What's wrong with the calendar (nothing is changed then), or null.</returns>
    public string? SetCalendar(Guid bodyId, Calendar? calendar)
    {
        if (FindBody(bodyId) is not Body body || !body.HasSurface)
        {
            return null;
        }

        if (calendar is not null
            && (calendar.Problem() ?? CalendarFitting.Problem(World.Bodies, body, calendar))
                is string problem)
        {
            return problem;
        }

        if (Equals(body.Calendar, calendar))
        {
            return null;
        }

        RecordUndo(calendar is null
            ? $"Remove {body.Name}'s Calendar"
            : $"Edit {body.Name}'s Calendar");
        body.Calendar = calendar;
        MarkChanged();
        TimeChanged?.Invoke();  // The date shown uses the calendar.
        return null;
    }

    /// <summary>
    /// The selected body's solstices and equinoxes around the current time (VISION.md CAL-03).
    /// They're worked out again only after an edit, a new selection, or a year of time passing.
    /// </summary>
    public SeasonTimeline SelectedSeasons
    {
        get
        {
            if (_seasons is var (world, version, bodyId, timeline) && world == World
                && version == _systemVersion && bodyId == SelectedBodyId
                && timeline.Covers(TimeDays))
            {
                return timeline;
            }

            SeasonTimeline fresh = SeasonTimeline.Around(World.Bodies, SelectedBody, TimeDays);
            _seasons = (World, _systemVersion, SelectedBodyId, fresh);
            return fresh;
        }
    }

    /// <summary>
    /// The selected body's eclipses around the current time (VISION.md EVT-01), or null while
    /// they're being worked out. Working them out takes several milliseconds, so it happens in
    /// the background, on a copy of the bodies, and only once something asks;
    /// <see cref="EclipsesReady"/> is raised when they're in. They're worked out again after an
    /// edit, a new selection, or half a year of clock time (meanwhile the last ones stay).
    /// </summary>
    public EclipseTimeline? SelectedEclipses
    {
        get
        {
            if (_eclipses is not var (world, version, bodyId, timeline) || world != World
                || version != _systemVersion || bodyId != SelectedBody.Id)
            {
                StartWorkingOutEclipses();
                return null;
            }

            if (!timeline.Covers(TimeDays))
            {
                StartWorkingOutEclipses();
            }

            return timeline;
        }
    }

    /// <summary>
    /// The selected body's meteor showers (VISION.md EVT-02), or null before they're first
    /// worked out. Like eclipses, they're worked out in the background, only once something
    /// asks, and again after an edit or a new selection; <see cref="MeteorShowersReady"/> is
    /// raised when they're in. While an edit's are being worked out, the body's last ones stay,
    /// so nothing flickers during a drag. They repeat every year, so time passing never
    /// changes them.
    /// </summary>
    public MeteorShowerTimeline? SelectedMeteorShowers
    {
        get
        {
            if (_showers is not var (world, version, bodyId, timeline) || world != World
                || bodyId != SelectedBody.Id)
            {
                StartWorkingOutMeteorShowers();
                return null;
            }

            if (version != _systemVersion)
            {
                StartWorkingOutMeteorShowers();
            }

            return timeline;
        }
    }

    /// <summary>
    /// The bodies a body could orbit: every other body except those orbiting it (which would
    /// make a loop).
    /// </summary>
    public IEnumerable<Body> PossibleParents(Guid bodyId)
    {
        var excluded = new HashSet<Guid>(
            SystemHierarchy.DescendantsOf(World.Bodies, bodyId).Select(b => b.Id)) { bodyId };
        Body body = FindBody(bodyId)!;
        return World.Bodies.Where(b => !excluded.Contains(b.Id)
            && SystemHierarchy.CanOrbit(body, b));
    }

    // ----- Maps -----

    /// <summary>
    /// Imports a map image and wraps it onto the selected body with the current map type.
    /// </summary>
    /// <exception cref="MapLoadException">The image couldn't be used.</exception>
    /// <exception cref="InvalidOperationException">
    /// The selected body is a star or comet.
    /// </exception>
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
        _fullMap = (assetName, map.Texture, map.Check);
        ShowMapsWithoutLoading();
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
        ShowMapsWithoutLoading();
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
        _systemVersion++;
        UpdateUnsavedState();
        Changed?.Invoke();
    }

    /// <summary>
    /// True while the Cut editor is open. Set by the editor, so New/Open can wait until it closes.
    /// </summary>
    public bool IsCutting { get; set; }

    // ----- Pieces -----

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

    // ----- Undo -----

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
    /// Takes back the last edit, selecting the body that was being edited so the change is
    /// visible. If that brings back a different map image, the image is reloaded (a few seconds
    /// for a large map).
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
        return RestoreAsync(_history.Undo(Snapshot()), description);
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
        return RestoreAsync(_history.Redo(Snapshot()), description);
    }

    /// <summary>
    /// Starts a gesture, such as dragging a piece: its edits (however many) become one undo
    /// step, recorded by <see cref="EndGesture"/>, and only if anything changed.
    /// </summary>
    /// <param name="description">What the gesture does, e.g. "Move Piece 1".</param>
    public void BeginGesture(string description)
    {
        EndGesture();
        _gesture = (description, Snapshot(), _editVersion);
        Changed?.Invoke();  // Undo isn't available mid-gesture.
    }

    /// <summary>Ends the gesture started by <see cref="BeginGesture"/>.</summary>
    public void EndGesture()
    {
        if (_gesture is not (string description, EditSnapshot before, int version))
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
        Guid selected;
        string? warning = null;
        (string Asset, Texture2D Texture, MapImageCheck Check)? fullMap = null;
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
                    LoadedMap map = await MapImageLoader.LoadAsync(
                        loaded.Assets[surfaceMap.AssetName], surfaceMap.AssetName);
                    fullMap = (surfaceMap.AssetName, map.Texture, map.Check);
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
        _savedBodies = unsaved ? null : CloneBodies(World.Bodies);
        _savedLore = unsaved ? null : LoreState.Of(World);
        _projectionWithoutMap = MapProjection.Mercator;
        _fullMap = fullMap;
        ShowWorld();
        Camera?.SetView(World.View);
        _editVersion++;
        _systemVersion++;
        SelectionChanged?.Invoke();
        TimeChanged?.Invoke();
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

    // Makes every globe show the right map: the selected body's at full size (loading it if
    // needed), the others as previews (loaded in the background).
    private async Task<string?> ShowMapsAsync()
    {
        if (!ShowMapsWithoutLoading()
            || SelectedBody.Surface.Map is not SurfaceMap map
            || _assets.GetValueOrDefault(map.AssetName) is not IAssetSource source)
        {
            return null;
        }

        Guid bodyId = SelectedBodyId;
        SetBusy(true);
        try
        {
            LoadedMap loaded = await MapImageLoader.LoadAsync(source, map.AssetName);
            _fullMap = (map.AssetName, loaded.Texture, loaded.Check);
            return null;
        }
        catch (MapLoadException error)
        {
            // The map stays in the world data, so saving keeps it.
            return $"The map image couldn't be shown: {error.Message}";
        }
        finally
        {
            SetBusy(false);
            if (SelectedBodyId == bodyId)
            {
                ShowMapsWithoutLoading();
                Changed?.Invoke();
            }
        }
    }

    // Applies every map already at hand, and starts loading missing previews. Returns true if
    // the selected body's full-size map still has to be loaded.
    private bool ShowMapsWithoutLoading()
    {
        bool needsFullMap = false;
        foreach (Body body in World.Bodies)
        {
            if (System?.SurfaceFor(body.Id) is not PlanetSurface surface)
            {
                _shownMaps.Remove(body.Id);
                continue;
            }

            string? asset = body.Surface.Map?.AssetName;
            bool selected = body.Id == SelectedBodyId;
            if (asset is null)
            {
                if (_shownMaps.Remove(body.Id))
                {
                    surface.ClearMap();
                }
            }
            else if (selected && _fullMap?.Asset == asset)
            {
                Show(body.Id, surface, asset, full: true, _fullMap.Value.Texture);
            }
            else if (selected)
            {
                needsFullMap = true;
                ShowPreviewIfReady(body.Id, surface, asset);
            }
            else if (_shownMaps.GetValueOrDefault(body.Id) != (asset, false))
            {
                ShowPreviewIfReady(body.Id, surface, asset);
            }
        }

        return needsFullMap;
    }

    private void ShowPreviewIfReady(Guid bodyId, PlanetSurface surface, string asset)
    {
        if (_previews.TryGetValue(asset, out Texture2D? preview))
        {
            Show(bodyId, surface, asset, full: false, preview);
        }
        else
        {
            _ = LoadPreviewAsync(asset, World.Id);
        }
    }

    private void Show(Guid bodyId, PlanetSurface surface, string asset, bool full, Texture2D map)
    {
        if (_shownMaps.GetValueOrDefault(bodyId) != (asset, full))
        {
            surface.SetMap(map);
            _shownMaps[bodyId] = (asset, full);
        }
    }

    // Loads a small preview in the background, then shows it wherever it's wanted.
    private async Task LoadPreviewAsync(string asset, Guid worldId)
    {
        if (!_previewsLoading.Add(asset)
            || _assets.GetValueOrDefault(asset) is not IAssetSource source)
        {
            return;
        }

        try
        {
            Texture2D preview = await MapImageLoader.LoadPreviewAsync(source, asset);
            if (World.Id == worldId)
            {
                _previews[asset] = preview;
                ShowMapsWithoutLoading();
            }
        }
        catch (MapLoadException error)
        {
            GD.PushWarning($"Couldn't load a map preview: {error.Message}");
        }
        finally
        {
            _previewsLoading.Remove(asset);
        }
    }

    private void ForgetMaps()
    {
        _shownMaps.Clear();
        _previews.Clear();
        _fullMap = null;
    }

    // Builds the system view for the open world, and shows every body's surface settings,
    // pieces, and maps.
    private void ShowWorld()
    {
        System?.Show(World, SelectedBodyId);
        _shownMaps.Clear();
        foreach (Body body in World.Bodies)
        {
            ShowSurfaceSettings(body);
            ShowPieces(body);
            ShowTerrain(body);
        }

        _ = ShowMapsAsync();
    }

    // Brings the system view up to date after bodies changed; new or rebuilt globes get their
    // surfaces filled in.
    private void SyncView()
    {
        if (System is null)
        {
            return;
        }

        foreach (Guid id in System.Sync(World))
        {
            _shownMaps.Remove(id);
            if (FindBody(id) is Body body)
            {
                ShowSurfaceSettings(body);
                ShowPieces(body);
                ShowTerrain(body);
            }
        }

        ShowMapsWithoutLoading();
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
        surface.SetAppearance(body.Appearance.Color.ToGodot(), body.Appearance.Pattern, body.Id);
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

    // The star a body belongs to: itself if it's a star, else the nearest star up its chain of
    // parents (null if there's none).
    private Body? StarOf(Body body)
    {
        for (Body? current = body; current is not null;
            current = current.Orbit is Orbit orbit ? FindBody(orbit.ParentId) : null)
        {
            if (IsStar(current))
            {
                return current;
            }
        }

        return null;
    }

    private static bool IsStar(Body body) => body.Kind == BodyKind.Star;

    // The body at the top of a body's chain of parents.
    private Body Root(Body body)
    {
        Body current = body;
        while (current.Orbit is Orbit orbit && FindBody(orbit.ParentId) is Body parent)
        {
            current = parent;
        }

        return current;
    }

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
        return (world.Bodies.FirstOrDefault(body => body.HasSurface)
            ?? world.Bodies[0]).Id;
    }

    // Works out the selected body's meteor showers in the background, like its eclipses.
    private async void StartWorkingOutMeteorShowers()
    {
        (World World, int Version, Guid BodyId) wanted =
            (World, _systemVersion, SelectedBody.Id);
        if (_showersUnderway is not null || _showersFailed == wanted)
        {
            return;
        }

        _showersUnderway = wanted;
        List<Body> bodies = CloneBodies(World.Bodies);
        Body body = bodies.First(b => b.Id == wanted.BodyId);
        try
        {
            MeteorShowerTimeline timeline =
                await Task.Run(() => MeteorShowerTimeline.For(bodies, body));
            _showers = (wanted.World, wanted.Version, wanted.BodyId, timeline);
        }
        catch (ArgumentException exception)
        {
            // An invalid system can't have showers; edits never leave one, so this is a bug.
            GD.PushError($"Couldn't work out meteor showers: {exception.Message}");
            _showersFailed = wanted;
        }
        finally
        {
            _showersUnderway = null;
        }

        MeteorShowersReady?.Invoke();
    }

    // Works out the selected body's eclipses in the background, unless that's already under
    // way (whoever asks next starts it again if the world moved on meanwhile), or it failed
    // for this same world.
    private async void StartWorkingOutEclipses()
    {
        (World World, int Version, Guid BodyId) wanted =
            (World, _systemVersion, SelectedBody.Id);
        if (_eclipsesUnderway is not null || _eclipsesFailed == wanted)
        {
            return;
        }

        _eclipsesUnderway = wanted;
        List<Body> bodies = CloneBodies(World.Bodies);
        Body body = bodies.First(b => b.Id == wanted.BodyId);
        double time = TimeDays;
        try
        {
            EclipseTimeline timeline =
                await Task.Run(() => EclipseTimeline.Around(bodies, body, time));
            _eclipses = (wanted.World, wanted.Version, wanted.BodyId, timeline);
        }
        catch (ArgumentException exception)
        {
            // An invalid system can't have eclipses; edits never leave one, so this is a bug.
            GD.PushError($"Couldn't work out eclipses: {exception.Message}");
            _eclipsesFailed = wanted;
        }
        finally
        {
            _eclipsesUnderway = null;
        }

        EclipsesReady?.Invoke();
    }

    private static List<Body> CloneBodies(IEnumerable<Body> bodies)
    {
        return [.. bodies.Select(body => body.Clone())];
    }

    private bool IsIdleForHistory =>
        !IsBusy && _gesture is null && !IsCalibrating && !IsCutting;

    private EditSnapshot Snapshot() =>
        new(SelectedBodyId, CloneBodies(World.Bodies), LoreState.Of(World));

    // Call just before changing the world. Inside a gesture, the gesture records it instead.
    private void RecordUndo(string description, object? mergeKey = null)
    {
        if (_gesture is null)
        {
            _history.Record(description, Snapshot(), mergeKey);
            ForgetUnusedTextures();
        }
    }

    // Puts back a snapshot from the history and selects the body that was being edited.
    private async Task<(string Description, string? Warning)> RestoreAsync(
        EditSnapshot snapshot, string description)
    {
        World.Bodies.Clear();
        World.Bodies.AddRange(CloneBodies(snapshot.Bodies));
        snapshot.Lore.RestoreTo(World);
        Guid select = FindBody(snapshot.SelectedBodyId) is not null
            ? snapshot.SelectedBodyId
            : DefaultSelection(World);
        bool selectionChanged = select != SelectedBodyId;
        SelectedBodyId = select;

        SyncView();
        foreach (Body body in World.Bodies)
        {
            ShowSurfaceSettings(body);
            ShowPieces(body);
            ShowTerrain(body);
        }

        if (selectionChanged)
        {
            System?.FlyTo(select);
            SelectionChanged?.Invoke();
        }

        ForgetUnusedTextures();
        MarkChanged();
        TimeChanged?.Invoke();  // A day length or name may have changed back.
        string? warning = await ShowMapsAsync();
        return (description, warning);
    }

    // Piece textures are kept while the world or its undo history still has the piece, so
    // undoing a delete is instant. The rest are freed.
    private void ForgetUnusedTextures()
    {
        var used = new HashSet<Guid>(_history.States.SelectMany(state => state.Bodies)
            .Concat(World.Bodies)
            .SelectMany(body => body.Surface.Pieces.Select(piece => piece.Id)));
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
            .SelectMany(state => state.Bodies)
            .SelectMany(body => body.Surface.Pieces.Select(piece => piece.AssetName)
                .Append(body.Surface.Map?.AssetName))
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

    // `systemChanged` false: only journal writing changed, so the simulation caches stay.
    // After any change to the system, realms move to where their branches hold them (VISION.md
    // BOD-02), then calendars that fit the world (CAL-02) adjust it again, as part of the same
    // edit (and undo step).
    private void MarkChanged(bool systemChanged = true)
    {
        _editVersion++;
        bool refitted = false;
        if (systemChanged)
        {
            Realms.Apply(World.Bodies);
            refitted = CalendarFitting.Apply(World.Bodies);
            _systemVersion++;
        }

        UpdateUnsavedState();
        Changed?.Invoke();
        if (refitted)
        {
            TimeChanged?.Invoke();  // A fitted day length changes the date shown.
        }
    }

    private void UpdateUnsavedState()
    {
        if (_savedBodies is null || _savedLore is null
            || _savedBodies.Count != World.Bodies.Count)
        {
            HasUnsavedChanges = true;
            return;
        }

        var saved = _savedBodies.ToDictionary(body => body.Id);
        HasUnsavedChanges = !_savedLore.Matches(World) || World.Bodies.Any(body =>
            !saved.TryGetValue(body.Id, out Body? before) || !body.HasSameContent(before));
    }

    private void CloseCurrentWorld()
    {
        Physics.Stop();
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
                "Stars and comets don't have maps. Select a planet or moon first.");
        }
    }

    // Every body at one moment, and which one was selected (so undo can show it), for undo.
    private sealed record EditSnapshot(Guid SelectedBodyId, List<Body> Bodies, LoreState Lore);
}
