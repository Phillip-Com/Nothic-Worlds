using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Session;

/// <summary>
/// Physics mode for the open world (VISION.md SIM-03): while it's on, the system view draws
/// the bodies where real gravity takes them (<see cref="GravitySimulation"/>), from the moment
/// it was switched on. Only the view follows it (owner's choice): seasons, calendars, eclipses,
/// and weather stay on the designed orbits, and the world isn't changed unless the user keeps
/// the result.
/// </summary>
/// <remarks>
/// The simulation runs on its own thread, following the world clock, so a long jump never
/// stalls a frame: until it catches up, the view shows where it has got to. Changing the
/// system (its bodies, sizes, densities, or orbits) starts it again from the new design at the
/// current time.
/// </remarks>
public sealed class PhysicsMode : IDisposable
{
    // Steps taken between looks at a new target time, so a changed clock is noticed quickly.
    private const int StepsPerRound = 2000;

    // Held while the simulation is used (it isn't thread-safe). The main thread never waits
    // on it each frame: it reads the published fields below instead.
    private readonly object _stepping = new();
    private readonly AutoResetEvent _wake = new(false);
    private volatile GravitySimulation? _simulation;
    private double _targetDays;  // Read and written with Volatile
    private volatile PhysicsSnapshot? _latest;
    private Thread? _worker;
    private volatile bool _stopping;
    private string _design = "";

    /// <summary>True while physics mode is on.</summary>
    public bool IsOn { get; private set; }

    /// <summary>When it was switched on (or last restarted), in days on the world clock.</summary>
    public double StartDays { get; private set; }

    /// <summary>Raised on the main thread when it's switched on or off, or restarted.</summary>
    public event Action? StateChanged;

    /// <summary>
    /// What the simulation last worked out, or null before its first answer. Safe to read
    /// from the main thread at any time.
    /// </summary>
    public PhysicsSnapshot? Latest => _latest is { } latest && latest.Simulation == _simulation
        ? latest
        : null;

    /// <summary>Switches physics mode on at <paramref name="timeDays"/>, from the design.</summary>
    public void Start(IReadOnlyList<Body> bodies, double timeDays)
    {
        // The simulation works from its own copies: the world may be edited meanwhile.
        List<Body> copies = [.. bodies.Select(body => body.Clone())];
        Volatile.Write(ref _targetDays, timeDays);
        _simulation = GravitySimulation.Start(copies, timeDays);
        _latest = null;
        _design = DesignOf(bodies);
        StartDays = timeDays;
        IsOn = true;
        EnsureWorker();
        _wake.Set();
        StateChanged?.Invoke();
    }

    /// <summary>Switches physics mode off: the view goes back to the designed orbits.</summary>
    public void Stop()
    {
        if (!IsOn)
        {
            return;
        }

        _simulation = null;
        _latest = null;
        IsOn = false;
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Starts again from the design at <paramref name="timeDays"/> if the system was changed
    /// since physics started (anything else, such as painting a map, is ignored).
    /// </summary>
    public void RestartIfRedesigned(IReadOnlyList<Body> bodies, double timeDays)
    {
        if (IsOn && DesignOf(bodies) != _design)
        {
            Start(bodies, timeDays);
        }
    }

    /// <summary>Asks the simulation to work out <paramref name="timeDays"/> next.</summary>
    public void Follow(double timeDays)
    {
        if (Volatile.Read(ref _targetDays) != timeDays)
        {
            Volatile.Write(ref _targetDays, timeDays);
            _wake.Set();
        }
    }

    /// <summary>
    /// The orbits the bodies are on under physics at <paramref name="timeDays"/>, to keep as
    /// their designed orbits. Null when physics mode is off.
    /// </summary>
    public KeptOrbits? Keep(double timeDays)
    {
        if (_simulation is not GravitySimulation simulation)
        {
            return null;
        }

        lock (_stepping)
        {
            return simulation.KeepAsOrbits(timeDays);
        }
    }

    public void Dispose()
    {
        _stopping = true;
        _wake.Set();
        _worker?.Join();
        _wake.Dispose();
    }

    private void EnsureWorker()
    {
        if (_worker is not null)
        {
            return;
        }

        _worker = new Thread(Work) { IsBackground = true, Name = "Physics mode" };
        _worker.Start();
    }

    // Works toward the clock's time a round at a time, publishing each answer, and sleeps
    // once it's there until the clock moves.
    private void Work()
    {
        while (!_stopping)
        {
            bool caughtUp = true;
            if (_simulation is GravitySimulation simulation)
            {
                double target = Volatile.Read(ref _targetDays);
                lock (_stepping)
                {
                    caughtUp = simulation.TryPositionsAt(target, StepsPerRound,
                        out Dictionary<Guid, Vector3D> positions);
                    _latest = new PhysicsSnapshot(simulation, positions, caughtUp,
                        [.. simulation.Collisions]);
                }

                caughtUp &= target == Volatile.Read(ref _targetDays);
            }

            if (caughtUp)
            {
                _wake.WaitOne();
            }
        }
    }

    // What physics depends on: each body's kind, shape, size, density, orbit, branch, and
    // tree. Two worlds with the same design simulate the same way.
    private static string DesignOf(IReadOnlyList<Body> bodies) => string.Join("|",
        bodies.Select(b => $"{b.Id},{b.Kind},{b.Shape},{b.RadiusKm},{b.DensityGramsPerCm3}," +
            $"{b.Orbit},{b.Branch},{b.Tree}"));
}
