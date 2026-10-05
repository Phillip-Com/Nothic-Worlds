using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Live weather on a planet or moon with air (VISION.md WTH-02; owner's choice: worked out from
/// the time, so any moment's weather is ready at once and always the same): clouds, rain and
/// snow, and wind, anywhere at any time. Built once for a body (and again when the world
/// changes), then asked for moments with <see cref="At"/>.
/// </summary>
/// <remarks>
/// <para><b>It stands on the climate</b> (<see cref="ClimateYear"/>): a year of weather every
/// 5° of latitude gives each moment's mean temperature and expected rain, and the star's
/// latitude, felt a month late, places the tropical rain belt and the storm belts just as the
/// weather pins' yearly charts do. Painted terrain adds its moisture (sea air wetter, deserts
/// dry) and its ground (ice and mountains colder).</para>
/// <para><b>Winds</b> blow in three belts each side of the equator, shifted with the seasons:
/// trade winds toward the west near the equator, westerlies in the middle latitudes, and
/// easterlies near the poles; storms add their own winds, turning counterclockwise in the
/// north and clockwise in the south (a body always spins counterclockwise about its own north
/// pole, so this holds even for one tipped over).</para>
/// <para><b>Clouds</b> are noise carried by the winds, more where the climate is wetter; and
/// storms, made up from the body's ID like everything else made up, so the same world always
/// has the same storms: cyclones forming in the storm belts and riding the westerlies toward
/// the poles, and tropical storms forming over warm water late in summer.</para>
/// <para><b>Rain and snow</b> fall from thick cloud and storms, as much as the climate there
/// brings: a year of it averages about the weather pins' yearly rain. Snow falls at or below
/// freezing.</para>
/// <para><b>Flat worlds</b> have no belts: a gentle wind, the face's even rain, and cyclones
/// anywhere.</para>
/// <para>Everything here is fixed once built (it keeps its own copy of what it needs from the
/// terrain), so moments can be worked out on other threads.</para>
/// </remarks>
public sealed class LiveWeather
{
    // Climate bands: a year of weather every this many degrees, from the south pole up.
    internal const double BandDegrees = 5.0;
    internal const int BandCount = 37;

    // The star's lagged latitude is kept for this many times through the year.
    private const int BeltSamples = 360;

    // Terrain is kept on coarse grids of latitude and longitude: moisture every 5°, the kind of
    // ground every degree.
    private const double MoistureCellDegrees = 5.0;
    private const double GroundCellDegrees = 1.0;

    private readonly ClimateYear[] _bands;
    private readonly double[] _beltLatitudes;
    private readonly double[]? _moisture;
    private readonly byte[]? _ground;

    private LiveWeather(ClimateYear[] bands, double[] beltLatitudes, double yearDays,
        double[]? moisture, byte[]? ground, Body body)
    {
        _bands = bands;
        _beltLatitudes = beltLatitudes;
        YearDays = yearDays;
        _moisture = moisture;
        _ground = ground;
        RadiusKm = body.RadiusKm;
        IsFlat = body.Shape == BodyShape.FlatDisc;
        Seed = SeededRandom.SeedOf(body.Id);
    }

    /// <summary>How long the body's year is, in standard days.</summary>
    public double YearDays { get; }

    internal double RadiusKm { get; }

    internal bool IsFlat { get; }

    internal ulong Seed { get; }

    /// <summary>
    /// Builds the live weather of a planet or moon, or returns null if it has none: it isn't a
    /// planet or moon, has no air (<see cref="Body.HasAtmosphere"/>), or has no star. The
    /// painted terrain is read now, with <paramref name="types"/> giving its climates.
    /// </summary>
    /// <exception cref="ArgumentException">The bodies' orbits are invalid.</exception>
    public static LiveWeather? For(
        IReadOnlyList<Body> bodies, Body body, IReadOnlyList<TerrainType> types)
    {
        if (!body.HasSurface || !body.HasAtmosphere
            || Seasons.StarFor(bodies, body) is not Body star)
        {
            return null;
        }

        var bands = new ClimateYear[BandCount];
        for (int i = 0; i < BandCount; i++)
        {
            var spot = new GeoCoordinate(-90 + i * BandDegrees, 0);
            bands[i] = ClimateYear.At(bodies, body, spot, 0)!;
        }

        double year = bands[0].YearDays;
        double step = year / BeltSamples;
        var declinations = new double[BeltSamples];
        for (int i = 0; i < BeltSamples; i++)
        {
            declinations[i] =
                Seasons.StarDeclinationDegrees(bodies, body, star, (i + 0.5) * step);
        }

        double[] belts = ClimateYear.Lagged(declinations, step, ClimateYear.HeatLagDays);
        bool painted = !body.Surface.Terrain.IsEmpty;
        return new LiveWeather(bands, belts, year,
            painted ? MoistureGrid(body, types) : null,
            painted ? GroundGrid(body, types) : null, body);
    }

    /// <summary>The weather everywhere on the body at a time (in standard days).</summary>
    public WeatherMoment At(double timeDays) => new(this, timeDays);

    /// <summary>The year's weather at a latitude band (0 is the south pole), on a day.</summary>
    internal ClimateDay BandDay(int band, double timeDays) => _bands[band].DayAt(timeDays);

    /// <summary>
    /// The star's latitude as the weather feels it (about a month late), in degrees.
    /// </summary>
    internal double FeltStarLatitude(double timeDays)
    {
        double position = Wrap(timeDays / YearDays) * BeltSamples - 0.5;
        int below = (int)Math.Floor(position);
        double t = position - below;
        double a = _beltLatitudes[(below % BeltSamples + BeltSamples) % BeltSamples];
        double b = _beltLatitudes[(below + 1) % BeltSamples];
        return a + (b - a) * t;
    }

    /// <summary>How much the terrain makes it rain here (1 without painted terrain).</summary>
    internal double MoistureAt(double latitude, double longitude)
    {
        if (_moisture is null)
        {
            return 1.0;
        }

        int rows = (int)(180 / MoistureCellDegrees), columns = (int)(360 / MoistureCellDegrees);
        double row = Math.Clamp((latitude + 90) / MoistureCellDegrees - 0.5, 0, rows - 1);
        double column = Wrap((longitude + 180) / 360) * columns - 0.5;
        int r0 = (int)Math.Floor(row), c0 = (int)Math.Floor(column);
        int r1 = Math.Min(r0 + 1, rows - 1);
        double tr = row - r0, tc = column - c0;
        double At(int r, int c) => _moisture[r * columns + (c % columns + columns) % columns];
        double bottom = At(r0, c0) + (At(r0, c0 + 1) - At(r0, c0)) * tc;
        double top = At(r1, c0) + (At(r1, c0 + 1) - At(r1, c0)) * tc;
        return bottom + (top - bottom) * tr;
    }

    /// <summary>The kind of ground here, or null where unpainted.</summary>
    internal ClimateKind? GroundAt(double latitude, double longitude)
    {
        if (_ground is null)
        {
            return null;
        }

        int columns = (int)(360 / GroundCellDegrees);
        int row = Math.Clamp((int)((latitude + 90) / GroundCellDegrees), 0, 180 - 1);
        int column = Math.Clamp((int)(Wrap((longitude + 180) / 360) * columns), 0, columns - 1);
        byte kind = _ground[row * columns + column];
        return kind == 0 ? null : (ClimateKind)(kind - 1);
    }

    // The share of a whole turn, wrapped into 0 to 1.
    private static double Wrap(double turns) => turns - Math.Floor(turns);

    // Each 5° cell's moisture, from the terrain at and around its middle.
    private static double[] MoistureGrid(Body body, IReadOnlyList<TerrainType> types)
    {
        int rows = (int)(180 / MoistureCellDegrees), columns = (int)(360 / MoistureCellDegrees);
        var grid = new double[rows * columns];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                var spot = new GeoCoordinate(-90 + (r + 0.5) * MoistureCellDegrees,
                    -180 + (c + 0.5) * MoistureCellDegrees);
                grid[r * columns + c] =
                    ClimateYear.Moisture(TerrainSurroundings.At(body, types, spot));
            }
        }

        return grid;
    }

    // Each 1° cell's kind of ground, at its middle: 0 unpainted, else the ClimateKind plus 1.
    private static byte[] GroundGrid(Body body, IReadOnlyList<TerrainType> types)
    {
        var kinds = types.ToDictionary(type => type.Code, type => type.Climate);
        int rows = 180, columns = 360;
        var grid = new byte[rows * columns];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3D direction = SphericalPolygon.ToUnit(new GeoCoordinate(
                    -90 + (r + 0.5) * GroundCellDegrees, -180 + (c + 0.5) * GroundCellDegrees));
                grid[r * columns + c] = kinds.TryGetValue(
                    body.Surface.Terrain.CodeAt(direction), out ClimateKind kind)
                    ? (byte)((int)kind + 1)
                    : (byte)0;
            }
        }

        return grid;
    }
}
