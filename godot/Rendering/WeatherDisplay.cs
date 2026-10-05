using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.Rendering;

/// <summary>
/// Draws live weather over every planet and moon with air (VISION.md WTH-02): keeps each one's
/// <see cref="LiveWeather"/> up to date with the world, and its globe fed with
/// <see cref="WeatherSnapshots"/> as the clock runs. All the working out happens off the main
/// thread, one snapshot at a time, and only for globes drawn big enough to see; each snapshot is
/// made a little ahead of the clock, so the globe can blend toward it.
/// </summary>
public partial class WeatherDisplay : Node
{
    // Globes smaller than this on screen (radius, in pixels) get no weather drawn.
    private const float MinGlobePixels = 20.0f;

    // How long after the world last changed to work its weather out again (edits often come in
    // a quick stream, such as a drag), in seconds.
    private const double RebuildDelaySeconds = 0.3;

    // A clock that moves farther than this in one go (a jump, not running) starts the weather
    // afresh instead of blending, in standard days.
    private const double JumpDays = 2.0;

    private readonly Dictionary<Guid, LiveWeather> _weather = [];
    private readonly Dictionary<Guid, WeatherSnapshots> _snapshots = [];
    private readonly HashSet<Guid> _stale = [];  // Showing weather from before the last change
    private readonly HashSet<string> _editors = [];  // Panels editing the surface, now open
    private Task<Dictionary<Guid, LiveWeather>>? _rebuilding;
    private Task<(Image Weather, Image Wind)>? _baking;
    private (Guid Body, double TimeDays, bool Alone, double StartedAt) _bakingFor;
    private double _changedAt = double.NaN;
    private double _lastBakeSeconds = 0.1;
    private double _lastClock = double.NaN;
    private double _clockRate;  // Standard days per second, smoothed
    private int _nextBody;
    private CloudDetail _detail = CloudDetail.High;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, for the globes and the world's style.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The camera, for how big the globes are on screen.</summary>
    [Export] public Camera3D? Camera { get; set; }

    /// <summary>
    /// Raised when the bodies' weather has been worked out again (after the world changed).
    /// </summary>
    public event Action? WeatherRebuilt;

    /// <summary>Whether clouds, rain, and snow show (View ▸ Clouds).</summary>
    public bool ShowClouds { get; set; } = true;

    /// <summary>Whether the wind shows (View ▸ Wind).</summary>
    public bool ShowWind { get; set; }

    /// <summary>
    /// Hides the clouds (with rain and snow) while a panel that edits the surface is open, so
    /// the ground can be seen (owner's choice: the Terrain and Map panels), or shows them again
    /// once none is. The wind still shows if it's on.
    /// </summary>
    /// <param name="panel">Which panel, by name.</param>
    /// <param name="open">Whether it's open now.</param>
    public void SetSurfaceEditing(string panel, bool open)
    {
        if (open)
        {
            _editors.Add(panel);
        }
        else
        {
            _editors.Remove(panel);
        }
    }

    /// <summary>
    /// How finely clouds are drawn (a quality setting, in File ▸ Settings; see
    /// <see cref="GraphicsSettings"/>).
    /// </summary>
    public CloudDetail Detail
    {
        get => _detail;
        set
        {
            _detail = value;
            _stale.UnionWith(_snapshots.Keys);  // Snapshots at the new detail, from now on
        }
    }

    /// <summary>
    /// A body's live weather as last worked out, or null if it has none (no air or no star) or
    /// it isn't ready yet. Safe to ask for moments from.
    /// </summary>
    public LiveWeather? WeatherOf(Guid bodyId) => _weather.GetValueOrDefault(bodyId);

    public override void _Ready()
    {
        if (Session is null || System is null || Camera is null)
        {
            GD.PushError("WeatherDisplay needs a world session, system view, and camera.");
            SetProcess(false);
            return;
        }

        Session.Changed += () => _changedAt = Now;
        Session.WorldClosed += _ => Clear();
        _changedAt = Now;
    }

    public override void _Process(double delta)
    {
        TrackClockRate(delta);
        FinishRebuild();
        if (!double.IsNaN(_changedAt) && Now - _changedAt >= RebuildDelaySeconds
            && _rebuilding is null)
        {
            StartRebuild();
        }

        FinishBake();
        double now = Session!.TimeDays;
        List<(Guid Id, WeatherSnapshots Snapshots)> shown = UpdateGlobes(now);
        if (_baking is null && shown.Count > 0)
        {
            StartBake(shown, now);
        }
    }

    private static double Now => Time.GetTicksMsec() / 1000.0;

    private void TrackClockRate(double delta)
    {
        double clock = Session!.TimeDays;
        if (!double.IsNaN(_lastClock) && delta > 0)
        {
            double rate = Math.Abs(clock - _lastClock) / delta;
            _clockRate = rate > 50 * Math.Max(_clockRate, 1)  // A jump, not the clock running
                ? _clockRate
                : 0.8 * _clockRate + 0.2 * rate;
        }

        _lastClock = clock;
    }

    // Works out every body's weather again, on another thread, from a copy of the world.
    private void StartRebuild()
    {
        _changedAt = double.NaN;
        List<Body> bodies = [.. Session!.World.Bodies.Select(body => body.Clone())];
        List<TerrainType> types = [.. Session.World.TerrainTypes];
        _rebuilding = Task.Run(() =>
        {
            var built = new Dictionary<Guid, LiveWeather>();
            foreach (Body body in bodies)
            {
                if (LiveWeather.For(bodies, body, types) is LiveWeather weather)
                {
                    built[body.Id] = weather;
                }
            }

            return built;
        });
    }

    private void FinishRebuild()
    {
        if (_rebuilding is not { IsCompleted: true } task)
        {
            return;
        }

        _rebuilding = null;
        if (task.IsFaulted)
        {
            // An invalid system shows no weather until it's fixed; the world is untouched.
            GD.PushWarning("Couldn't work out the weather: "
                + task.Exception?.GetBaseException().Message);
            return;
        }

        _weather.Clear();
        foreach ((Guid id, LiveWeather weather) in task.Result)
        {
            _weather[id] = weather;
        }

        foreach (Guid id in _snapshots.Keys.Where(id => !_weather.ContainsKey(id)).ToList())
        {
            Forget(id);
        }

        // Snapshots of the old weather blend into the new; ask for new ones now.
        _stale.UnionWith(_snapshots.Keys);
        WeatherRebuilt?.Invoke();
    }

    // Draws the weather on every globe with it that's big enough to see, hides it on the rest,
    // and returns those showing it.
    private List<(Guid Id, WeatherSnapshots Snapshots)> UpdateGlobes(double now)
    {
        var shown = new List<(Guid, WeatherSnapshots)>();
        foreach (Guid id in _weather.Keys)
        {
            if (System!.SurfaceFor(id) is not PlanetSurface surface)
            {
                continue;
            }

            bool clouds = ShowClouds && _editors.Count == 0;
            if (!(clouds || ShowWind) || !surface.IsVisibleInTree() || !IsBigEnough(surface))
            {
                surface.HideWeather();
                continue;
            }

            if (!_snapshots.TryGetValue(id, out WeatherSnapshots? snapshots))
            {
                snapshots = new WeatherSnapshots();
                _snapshots[id] = snapshots;
            }

            surface.SetWeatherLook(clouds, ShowWind, _detail);
            if (snapshots.HasWeather)
            {
                snapshots.ShowOn(surface, now);
            }
            else
            {
                surface.HideWeather();
            }

            shown.Add((id, snapshots));
        }

        return shown;
    }

    // Stops drawing a body's weather and drops its snapshots.
    private void Forget(Guid id)
    {
        _snapshots.Remove(id);
        _stale.Remove(id);
        System?.SurfaceFor(id)?.HideWeather();
    }

    // Starts the snapshot most needed: a globe with none, after a jump, or one the clock has
    // caught up with; taking turns between globes.
    private void StartBake(List<(Guid Id, WeatherSnapshots Snapshots)> shown, double now)
    {
        for (int i = 0; i < shown.Count; i++)
        {
            (Guid id, WeatherSnapshots snapshots) = shown[(_nextBody + i) % shown.Count];
            bool jumped = snapshots.HasWeather
                && (now < snapshots.OldTime - 1e-9 || Math.Abs(now - snapshots.NewTime) > JumpDays);
            bool stale = _stale.Contains(id);
            bool behind = snapshots.HasWeather && now >= snapshots.NewTime && _clockRate > 0;
            if (snapshots.HasWeather && !jumped && !stale && !behind)
            {
                continue;
            }

            // Far enough ahead that the clock won't have passed it before it's ready.
            double lead = _clockRate * Math.Max(_lastBakeSeconds, 0.05) * 1.5;
            double target = !snapshots.HasWeather || jumped ? now : now + lead;
            _stale.Remove(id);
            LiveWeather weather = _weather[id];
            int width = (int)_detail;
            _bakingFor = (id, target, !snapshots.HasWeather || jumped, Now);
            _baking = Task.Run(() => WeatherImages.Of(weather.At(target), width));
            _nextBody = (_nextBody + i + 1) % shown.Count;
            return;
        }
    }

    private void FinishBake()
    {
        if (_baking is not { IsCompleted: true } task)
        {
            return;
        }

        _baking = null;
        _lastBakeSeconds = Now - _bakingFor.StartedAt;
        if (task.IsFaulted)
        {
            GD.PushWarning("Couldn't draw the weather: "
                + task.Exception?.GetBaseException().Message);
            return;
        }

        if (_snapshots.TryGetValue(_bakingFor.Body, out WeatherSnapshots? snapshots))
        {
            (Image weather, Image wind) = task.Result;
            snapshots.Add(weather, wind, _bakingFor.TimeDays, _bakingFor.Alone);
        }
    }

    private void Clear()
    {
        foreach (Guid id in _snapshots.Keys.ToList())
        {
            Forget(id);
        }

        _weather.Clear();
        _changedAt = Now;
    }

    private bool IsBigEnough(PlanetSurface globe)
    {
        // Standing on it, the globe fills the view (and its camera is set aside).
        if (System!.StandingOn is Guid standing && System.SurfaceFor(standing) == globe)
        {
            return true;
        }

        if (Camera!.IsPositionBehind(globe.GlobalPosition))
        {
            return false;
        }

        Vector2 center = Camera.UnprojectPosition(globe.GlobalPosition);
        Vector3 edge = globe.GlobalPosition
            + Camera.GlobalBasis.X * globe.GlobalTransform.Basis.X.Length();
        return center.DistanceTo(Camera.UnprojectPosition(edge)) >= MinGlobePixels;
    }
}
