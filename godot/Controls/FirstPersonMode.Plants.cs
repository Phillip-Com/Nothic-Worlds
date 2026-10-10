using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;
using NothicWorlds.Rendering;
using NothicWorlds.Session;
using NothicWorlds.UI;

namespace NothicWorlds.Controls;

/// <summary>
/// Plants while standing (VISION.md REN-06; owner's choices, 2026-10-10): the trees, bushes,
/// small plants, stones, and grass each terrain type grows (its Plants), drawn by
/// <see cref="Rendering.StandingPlants"/> on the ground tiles, as many as File ▸ Settings,
/// Standing plants says, shown or hidden by the remembered Plants switch (V).
/// </summary>
public partial class FirstPersonMode
{
    // How long the help line says why V did nothing, in seconds.
    private const double PlantsNoteSeconds = 4;

    // The short grass's own colors (sRGB): green, and yellowed on dry, sandy, or stony
    // ground; and how far each is taken toward its terrain type's color.
    private static readonly Color _greenGrass = new(0.36f, 0.5f, 0.18f);
    private static readonly Color _dryGrass = new(0.66f, 0.6f, 0.36f);
    private const float GrassTint = 0.35f;

    private StandingPlantDetail _standingPlants = StandingPlantDetail.Standard;
    private PlantModels? _plantModels;    // Loaded when plants are first wanted standing
    private bool _plantModelsFailed;      // They couldn't be (said once)
    private StandingPlants? _plants;
    private double _plantsNote;           // Seconds left saying why V did nothing

    // What grows where, as last given to the plants: the terrain painted on the body and its
    // types' plants, numbered so the plants know when it changed.
    private (TerrainGrid Terrain, TerrainType[] Types)? _plantCover;
    private long _plantCoverVersion;

    /// <summary>
    /// How many plants are drawn around the eye, and how far (File ▸ Settings, Standing
    /// plants): changed while standing, they're placed again.
    /// </summary>
    public StandingPlantDetail StandingPlants
    {
        get => _standingPlants;
        set
        {
            if (value == _standingPlants)
            {
                return;
            }

            _standingPlants = value;
            FreePlants();
            SyncPlantsSwitch();
        }
    }

    // Sets up the Plants switch: remembered on this computer (owner's choice).
    private void ReadyPlants()
    {
        _hud!.PlantsSwitch.Toggled += on =>
        {
            AppSettings.ShowPlants = on;
            if (_plants is not null)
            {
                _plants.Visible = on;
            }
        };
    }

    // Shows the Plants switch as it is, and disables it (saying why) when plants are off.
    private void SyncPlantsSwitch()
    {
        if (_hud is null)
        {
            return;
        }

        _hud.PlantsSwitch.SetPressedNoSignal(AppSettings.ShowPlants);
        DisabledTip.Apply(_hud.PlantsSwitch,
            "Show or hide the trees, bushes, and grass here (how many: File ▸ Settings, " +
            "Standing plants)",
            _standingPlants == StandingPlantDetail.Off
                ? "Plants are off: turn them on in File ▸ Settings, Standing plants"
                : null);
    }

    // V: the Plants switch, or, with plants off, a note in the help line saying why not.
    private void TogglePlants()
    {
        if (_hud!.PlantsSwitch.Disabled)
        {
            _plantsNote = PlantsNoteSeconds;
            ShowHelp();
            return;
        }

        _hud.PlantsSwitch.ButtonPressed = !_hud.PlantsSwitch.ButtonPressed;
    }

    // The help line's note on the plants, if V was just refused.
    private string PlantsNote() => _plantsNote > 0
        ? " · Plants are off (File ▸ Settings, Standing plants)"
        : "";

    // Counts down the note on the plants, taking it off the help line when it's done.
    private void AgePlantsNote(double delta)
    {
        if (_plantsNote > 0 && (_plantsNote -= delta) <= 0)
        {
            ShowHelp();
        }
    }

    // Keeps the plants placed on the ground tiles around an eye at `eye`, `heightMeters`
    // above the ground at `eyeBase` (as the tiles' surface has them: a globe's, or a flat
    // world's top face), making them the first time they're wanted. None where the tiles
    // aren't shown (a flat world's rim and underside, a carved globe).
    private void KeepPlants(PlanetSurface globe, Body body, GroundTiles tiles, Vector3D eye,
        Vector3D eyeBase, double heightMeters)
    {
        bool wanted = _standingPlants != StandingPlantDetail.Off && tiles.Visible
            && tiles.HasGround && AppSettings.ShowPlants;
        if (!wanted)
        {
            if (_plants is not null)
            {
                _plants.Visible = false;
            }

            return;
        }

        if (_plants is null && !MakePlants(body))
        {
            return;
        }

        _plants!.Visible = true;
        _plants.Update(eye, eyeBase, heightMeters,
            PlantGroundFor(globe, body, tiles, PlantCoverVersion(body)));
    }

    // Hides the plants (where the ground tiles are hidden).
    private void HidePlants()
    {
        if (_plants is not null)
        {
            _plants.Visible = false;
        }
    }

    // Puts the plants in the scene, relative to the eye.
    private void PlacePlants(Body body, double time, Vector3D eye, double displayRadius)
    {
        _plants?.Place(point => At(body, time, eye, displayRadius, point),
            body.RadiusKm * 1000 / displayRadius);
    }

    // Makes the plants for the body stood on, loading the models the first time. If they
    // can't be loaded, there are none, and the log says why.
    private bool MakePlants(Body body)
    {
        if (_plantModels is null && !_plantModelsFailed)
        {
            try
            {
                _plantModels = PlantModels.Load();
            }
            catch (InvalidOperationException error)
            {
                _plantModelsFailed = true;
                GD.PushError($"Couldn't load the plants: {error.Message}");
            }
        }

        if (_plantModels is null)
        {
            return false;
        }

        ITileSurface surface = _flat is null ? new GlobeTileSurface() : new FlatTopTileSurface();
        _plants = new StandingPlants(_plantModels, surface, body.RadiusKm * 1000,
            TerrainRelief.SeedFor(body.Id), _standingPlants);
        AddChild(_plants);
        return true;
    }

    // Lets the plants go (on leaving, or when their detail changes); their models too, on
    // leaving.
    private void FreePlants(bool models = false)
    {
        _plants?.QueueFree();
        _plants = null;
        _plantCover = null;
        if (models)
        {
            _plantModels = null;
        }
    }

    // A number that changes whenever what grows where on the body does.
    private long PlantCoverVersion(Body body)
    {
        TerrainGrid terrain = body.Surface.Terrain;
        List<TerrainType> types = Session!.World.TerrainTypes;
        if (_plantCover is not var (lastTerrain, lastTypes)
            || !ReferenceEquals(lastTerrain, terrain) || !lastTypes.SequenceEqual(types))
        {
            _plantCover = (terrain, types.ToArray());
            _plantCoverVersion++;
        }

        return _plantCoverVersion;
    }

    // What the plants are placed against: the ground tiles drawn, the water and rivers, the
    // snow line, and each terrain type's plants (all safe to read on a worker). Terrain,
    // water, and rivers are found by a point's globe direction (on a flat world, the
    // direction its top face is mapped from), heights above the tiles' base: a globe's
    // radius, or just over a flat world's top face.
    private PlantGround PlantGroundFor(PlanetSurface globe, Body body, GroundTiles tiles,
        long coverVersion)
    {
        double radiusMeters = body.RadiusKm * 1000;
        Func<Vector3D, Vector3D> toDirection = _flat is null ? Unit : FlatDisc.DirectionFor;
        double baseHeight = _flat is null ? 1 : FlatGroundLift;
        (TerrainGrid terrain, TerrainType[] types) = _plantCover!.Value;
        var covers = types.ToDictionary(type => type.Code, type => (type.Plants,
            GrassColor(type)));
        RiverCarving? carving = _profiles is { Carving: { IsEmpty: false } some } ? some : null;
        double? snowLine = GroundMaterials.SnowLineMeters(body);
        bool hasWater = globe.WaterRadius is not null || globe.HasLakes;
        return new PlantGround(
            HashCode.Combine(coverVersion, tiles.ShownVersion, snowLine),
            coverVersion,
            tiles.ShownHeights(),
            carving is null
                ? static _ => false
                : (Func<Vector3D, bool>)(point =>
                    carving.BeyondBanksMeters(toDirection(point), 1) <= 0),
            hasWater
                ? (point, height) => globe.WaterRadiusAt(toDirection(point)) is double water
                    && baseHeight + water - 1 > height
                : (_, _) => false,
            snowLine is double line
                ? (point, height, below) => (height - baseHeight) * radiusMeters + below
                    > GroundMaterials.SnowLineAt(line, Latitude(toDirection(point)))
                : (_, _, _) => false,
            point => covers.TryGetValue(terrain.CodeAt(toDirection(point)), out var cover)
                ? cover
                : (PlantCover.None, default));
    }

    // A terrain type's short grass: green or dry by its ground, taken partway toward its
    // color, in linear light.
    private static Color GrassColor(TerrainType type)
    {
        Color own = type.Ground is GroundKind.DryGrass or GroundKind.Sand or GroundKind.Gravel
            or GroundKind.Rock
            ? _dryGrass
            : _greenGrass;
        return own.Lerp(type.Color.ToGodot(), GrassTint).SrgbToLinear();
    }

    // A point's latitude on the body, in degrees.
    private static double Latitude(Vector3D point) =>
        double.RadiansToDegrees(Math.Asin(Math.Clamp(point.Y / point.Length, -1, 1)));
}
