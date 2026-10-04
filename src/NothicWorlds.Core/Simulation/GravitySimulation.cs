using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Physics mode (VISION.md SIM-03): the system moved by real gravity instead of its designed
/// orbits, from the moment it's switched on. Every body pulls on every other by its mass
/// (<see cref="BodyMass"/>). The world itself is never changed.
/// </summary>
/// <remarks>
/// <para>Owner's choices: each body starts where its design puts it, moving at the speed gravity
/// would give that orbit (so a well-built system holds together); when two bodies touch, the
/// smaller merges into the bigger and it's logged. Realms stay on their world tree's branches,
/// carried round as designed; they pull on others but aren't pulled.</para>
/// <para>Deterministic: the simulation takes its own steps (each a small fraction of the
/// quickest orbit at that moment, so close passes get finer steps), whatever times it's asked
/// for, and positions between two steps are interpolated. Asking for the same time always
/// gives the same answer. Going back in time restarts from the nearest saved checkpoint.</para>
/// <para>Not thread-safe: use it from one thread at a time.</para>
/// </remarks>
public sealed class GravitySimulation
{
    // G in km³ / (kg day²).
    private const double Gravity = 6.674e-20 * 86_400 * 86_400;

    // Steps per trip round the quickest orbit (or close pass) at each moment.
    private const double StepsPerOrbit = 200;
    private const double ShortestStepDays = 1e-7;
    private const double LoneStepDays = 365.25;  // With nothing to pull on anything
    private const int CheckpointEvery = 256;

    private readonly Body[] _bodies;
    private readonly int[] _carrier;  // For a realm, its tree's index; otherwise -1
    private readonly double[] _reachKm;
    private readonly List<State> _checkpoints = [];
    private readonly List<Collision> _collisions = [];
    private long _furthestStep;
    private State _before;
    private State _after;

    private GravitySimulation(Body[] bodies, int[] carrier, State start)
    {
        _bodies = bodies;
        _carrier = carrier;
        _reachKm = [.. bodies.Select(OrbitStability.ReachKm)];
        StartDays = start.Time;
        start.Accelerations = Accelerations(start);
        _checkpoints.Add(start.Clone());
        _before = start;
        _after = start.Clone();
        StepInto(_before, _after);
    }

    /// <summary>When physics mode started, in days on the world clock.</summary>
    public double StartDays { get; }

    /// <summary>
    /// Every collision so far (up to the furthest time worked out), in order. Going back in time
    /// keeps them: the same ones happen again.
    /// </summary>
    public IReadOnlyList<Collision> Collisions => _collisions;

    /// <summary>
    /// Starts physics mode at <paramref name="timeDays"/>, from where the bodies' designed orbits
    /// put them, each moving at gravity's speed for its orbit.
    /// </summary>
    /// <exception cref="ArgumentException">The bodies' orbits are invalid.</exception>
    public static GravitySimulation Start(IReadOnlyList<Body> bodies, double timeDays)
    {
        Dictionary<Guid, Vector3D> positions = SystemPositions.At(bodies, timeDays);
        var byId = bodies.ToDictionary(b => b.Id);
        var velocities = new Dictionary<Guid, Vector3D>();
        Body[] list = [.. bodies];
        var index = list.Select((body, i) => (body.Id, i)).ToDictionary(p => p.Id, p => p.i);
        int[] carrier = [.. list.Select(body => body.Branch is not null
            && body.Orbit is Orbit orbit && byId[orbit.ParentId].Kind == BodyKind.WorldTree
                ? index[orbit.ParentId]
                : -1)];
        var start = new State(list.Length) { Time = timeDays };
        for (int i = 0; i < list.Length; i++)
        {
            start.Positions[i] = positions[list[i].Id];
            start.Velocities[i] = StartVelocity(bodies, list[i], byId, velocities, timeDays);
            start.Masses[i] = BodyMass.Kg(list[i]);
            start.Alive[i] = true;
        }

        StopDrifting(start, carrier);
        return new GravitySimulation(list, carrier, start);
    }

    /// <summary>
    /// Every remaining body's position at <paramref name="timeDays"/>, in km from the system's
    /// center (bodies merged into others are left out). Times before the start give the start.
    /// </summary>
    /// <param name="timeDays">The time on the world clock.</param>
    /// <param name="maxSteps">The most steps to take now, to keep each call quick.</param>
    /// <param name="positions">
    /// The positions at that time, or, if it wasn't reached in time, at the furthest time that
    /// was.
    /// </param>
    /// <returns>True if the time was reached.</returns>
    public bool TryPositionsAt(
        double timeDays, int maxSteps, out Dictionary<Guid, Vector3D> positions)
    {
        bool reached = Bracket(timeDays, maxSteps);
        double time = reached ? Math.Max(timeDays, StartDays) : _after.Time;
        positions = [];
        for (int i = 0; i < _bodies.Length; i++)
        {
            if (_before.Alive[i] && _after.Alive[i])
            {
                positions[_bodies[i].Id] = Interpolated(i, time).Position;
            }
        }

        return reached;
    }

    /// <summary>
    /// The orbits the bodies are on at <paramref name="timeDays"/> under physics, around their
    /// designed parents, to keep as their designed orbits (owner's choice: "Keep as orbits").
    /// Realms stay on their branches, so they're in neither list.
    /// </summary>
    public KeptOrbits KeepAsOrbits(double timeDays)
    {
        Bracket(timeDays, int.MaxValue);
        double time = Math.Max(timeDays, StartDays);
        var index = _bodies.Select((body, i) => (body.Id, i)).ToDictionary(p => p.Id, p => p.i);
        var orbits = new Dictionary<Guid, Orbit>();
        var notKept = new Dictionary<Guid, string>();
        for (int i = 0; i < _bodies.Length; i++)
        {
            if (_bodies[i].Orbit is not Orbit design || _carrier[i] >= 0)
            {
                continue;
            }

            int parent = index[design.ParentId];
            string? gone = Absorbed(i, time)
                ?? (Absorbed(parent, time) is string parentGone
                    ? $"{_bodies[parent].Name} {parentGone}"
                    : null);
            if (gone is not null)
            {
                notKept[_bodies[i].Id] = gone;
                continue;
            }

            (Vector3D position, Vector3D velocity) = Interpolated(i, time);
            (Vector3D parentPosition, Vector3D parentVelocity) = Interpolated(parent, time);
            double pull = Gravity * (_after.Masses[i] + _after.Masses[parent]);
            (Orbit? orbit, string? problem) = OrbitElements.FromMotion(position - parentPosition,
                velocity - parentVelocity, pull, time, design);
            if (orbit is not null)
            {
                orbits[_bodies[i].Id] = orbit;
            }
            else
            {
                notKept[_bodies[i].Id] = $"{problem} from {_bodies[parent].Name}";
            }
        }

        return new KeptOrbits(orbits, notKept);
    }

    // Steps until the two states either side of the time are known. False if that would take
    // more than maxSteps.
    private bool Bracket(double timeDays, int maxSteps)
    {
        if (timeDays < _before.Time && _before.Time > StartDays)
        {
            State checkpoint = _checkpoints.Last(c => c.Time <= Math.Max(timeDays, StartDays));
            _before = checkpoint.Clone();
            StepInto(_before, _after);
        }

        for (int steps = 0; timeDays > _after.Time; steps++)
        {
            if (steps >= maxSteps)
            {
                return false;
            }

            (_before, _after) = (_after, _before);
            StepInto(_before, _after);
        }

        return true;
    }

    // How a body merged away by this time, or null if it's still there.
    private string? Absorbed(int body, double timeDays)
    {
        Guid id = _bodies[body].Id;
        Collision? merged = _collisions.FirstOrDefault(c => c.AbsorbedId == id
            && c.TimeDays <= timeDays);
        return merged is null
            ? null
            : $"merged into {_bodies.First(b => b.Id == merged.IntoId).Name}";
    }

    // A body's position and velocity between the two bracketing steps (cubic Hermite, from
    // both ends' positions and velocities, so the path is smooth through each step).
    private (Vector3D Position, Vector3D Velocity) Interpolated(int i, double timeDays)
    {
        double span = _after.Time - _before.Time;
        double s = span > 0 ? Math.Clamp((timeDays - _before.Time) / span, 0, 1) : 1;
        double s2 = s * s;
        double s3 = s2 * s;
        Vector3D p0 = _before.Positions[i];
        Vector3D p1 = _after.Positions[i];
        Vector3D v0 = _before.Velocities[i] * span;
        Vector3D v1 = _after.Velocities[i] * span;
        Vector3D position = p0 * (2 * s3 - 3 * s2 + 1) + v0 * (s3 - 2 * s2 + s)
            + p1 * (-2 * s3 + 3 * s2) + v1 * (s3 - s2);
        Vector3D velocity = span > 0
            ? (p0 * (6 * s2 - 6 * s) + v0 * (3 * s2 - 4 * s + 1) + p1 * (-6 * s2 + 6 * s)
                + v1 * (3 * s2 - 2 * s)) * (1 / span)
            : _after.Velocities[i];
        return (position, velocity);
    }

    // One step (kick, drift, kick: the "leapfrog" method, which keeps orbits from slowly
    // gaining or losing energy), then any collisions.
    private void StepInto(State from, State into)
    {
        from.CopyTo(into);
        double step = StepDays(from);
        into.Time = from.Time + step;
        into.Step = from.Step + 1;
        for (int i = 0; i < _bodies.Length; i++)
        {
            if (into.Alive[i] && _carrier[i] < 0)
            {
                into.Velocities[i] += from.Accelerations[i] * (step / 2);
                into.Positions[i] += into.Velocities[i] * step;
            }
        }

        PlaceCarried(into);
        Vector3D[] pulls = Accelerations(into);
        for (int i = 0; i < _bodies.Length; i++)
        {
            if (into.Alive[i] && _carrier[i] < 0)
            {
                into.Velocities[i] += pulls[i] * (step / 2);
            }
        }

        into.Accelerations = pulls;
        bool firstTime = into.Step > _furthestStep;
        if (Collide(from, into, firstTime))
        {
            into.Accelerations = Accelerations(into);
        }

        // Again, now the trees have their full new velocities (and any merges are done).
        PlaceCarried(into);

        if (firstTime)
        {
            _furthestStep = into.Step;
            if (into.Step % CheckpointEvery == 0)
            {
                _checkpoints.Add(into.Clone());
            }
        }
    }

    // Realms ride their trees' branches as designed.
    private void PlaceCarried(State state)
    {
        for (int i = 0; i < _bodies.Length; i++)
        {
            if (_carrier[i] >= 0 && state.Alive[i])
            {
                int tree = _carrier[i];
                state.Positions[i] = state.Positions[tree]
                    + OrbitMath.OffsetFromParent(_bodies[i].Orbit!, state.Time);
                state.Velocities[i] = state.Velocities[tree]
                    + DesignedVelocity(_bodies[i].Orbit!, state.Time);
            }
        }
    }

    // The pull on each free body from every other body, in km / day².
    private Vector3D[] Accelerations(State state)
    {
        var pulls = new Vector3D[_bodies.Length];
        for (int i = 0; i < _bodies.Length; i++)
        {
            if (!state.Alive[i] || _carrier[i] >= 0)
            {
                continue;
            }

            for (int j = 0; j < _bodies.Length; j++)
            {
                if (j == i || !state.Alive[j])
                {
                    continue;
                }

                Vector3D toOther = state.Positions[j] - state.Positions[i];
                double distance = toOther.Length;
                if (distance > 0)
                {
                    pulls[i] += toOther
                        * (Gravity * state.Masses[j] / (distance * distance * distance));
                }
            }
        }

        return pulls;
    }

    // A step short enough for the quickest orbit or close pass now: a fraction of the time
    // two bodies would take to swing round each other at their current distance.
    private double StepDays(State state)
    {
        double quickest = double.PositiveInfinity;
        for (int i = 0; i < _bodies.Length; i++)
        {
            for (int j = i + 1; j < _bodies.Length; j++)
            {
                if (Interacting(state, i, j))
                {
                    double distance = (state.Positions[j] - state.Positions[i]).Length;
                    double swing = Math.Sqrt(distance * distance * distance
                        / (Gravity * (state.Masses[i] + state.Masses[j])));
                    quickest = Math.Min(quickest, swing);
                }
            }
        }

        return double.IsInfinity(quickest)
            ? LoneStepDays
            : Math.Max(ShortestStepDays, quickest * 2 * Math.PI / StepsPerOrbit);
    }

    // Two remaining bodies whose meeting matters: not two realms, nor a realm and its tree.
    private bool Interacting(State state, int i, int j) =>
        state.Alive[i] && state.Alive[j]
        && !(_carrier[i] >= 0 && _carrier[j] >= 0)
        && _carrier[i] != j && _carrier[j] != i;

    // Merges bodies that touched during the step: the smaller into the bigger, keeping their
    // total momentum. Fast bodies can cross each other's paths within one step, so it's their
    // closest approach along the step that counts, not just where they end up.
    private bool Collide(State from, State state, bool log)
    {
        bool any = false;
        for (int i = 0; i < _bodies.Length; i++)
        {
            for (int j = i + 1; j < _bodies.Length; j++)
            {
                if (Interacting(state, i, j)
                    && ClosestDuringStep(from, state, i, j) is double along)
                {
                    bool iBigger = state.Masses[i] >= state.Masses[j];
                    double time = from.Time + along * (state.Time - from.Time);
                    Merge(state, iBigger ? j : i, iBigger ? i : j, log ? time : null);
                    any = true;
                }
            }
        }

        return any;
    }

    // How far through the step (0 to 1) two bodies first touched, or null if they didn't,
    // taking each to move in a straight line across the step.
    private double? ClosestDuringStep(State from, State to, int i, int j)
    {
        Vector3D start = from.Positions[j] - from.Positions[i];
        Vector3D change = to.Positions[j] - to.Positions[i] - start;
        double touching = _reachKm[i] + _reachKm[j];
        double along = change.Dot(change) > 0
            ? Math.Clamp(-start.Dot(change) / change.Dot(change), 0, 1)
            : 1;
        return (start + change * along).Length < touching ? along : null;
    }

    private void Merge(State state, int absorbed, int into, double? logAt)
    {
        double total = state.Masses[into] + state.Masses[absorbed];
        if (_carrier[into] < 0)
        {
            state.Velocities[into] = (state.Velocities[into] * state.Masses[into]
                + state.Velocities[absorbed] * state.Masses[absorbed]) * (1 / total);
        }

        state.Masses[into] = total;
        state.Alive[absorbed] = false;
        if (logAt is double time)
        {
            _collisions.Add(new Collision(time, _bodies[absorbed].Id, _bodies[into].Id));
        }

        // Realms on a tree that's swallowed go with it.
        for (int k = 0; k < _bodies.Length; k++)
        {
            if (_carrier[k] == absorbed && state.Alive[k])
            {
                Merge(state, k, into, logAt);
            }
        }
    }

    // A body's starting velocity (km / day): its parent's, plus its designed motion around the
    // parent sped up or slowed to gravity's period (the same ellipse, so the same place).
    private static Vector3D StartVelocity(IReadOnlyList<Body> bodies, Body body,
        Dictionary<Guid, Body> byId, Dictionary<Guid, Vector3D> known, double timeDays)
    {
        if (known.TryGetValue(body.Id, out Vector3D velocity))
        {
            return velocity;
        }

        velocity = Vector3D.Zero;
        if (body.Orbit is Orbit orbit)
        {
            Vector3D around = DesignedVelocity(orbit, timeDays);
            if (OrbitStability.NaturalPeriodDays(bodies, body) is double natural)
            {
                around *= orbit.PeriodDays / natural;
            }

            velocity = StartVelocity(bodies, byId[orbit.ParentId], byId, known, timeDays) + around;
        }

        known[body.Id] = velocity;
        return velocity;
    }

    // How fast a body moves along its designed orbit, in km / day (from two nearby moments).
    private static Vector3D DesignedVelocity(Orbit orbit, double timeDays)
    {
        double moment = orbit.PeriodDays * 1e-6;
        return (OrbitMath.OffsetFromParent(orbit, timeDays + moment)
            - OrbitMath.OffsetFromParent(orbit, timeDays - moment)) * (1 / (2 * moment));
    }

    // Takes away the whole system's overall drift, so its balance point stays put (otherwise
    // the planets' combined momentum would carry everything slowly away).
    private static void StopDrifting(State state, int[] carrier)
    {
        Vector3D momentum = Vector3D.Zero;
        double mass = 0;
        for (int i = 0; i < state.Masses.Length; i++)
        {
            if (carrier[i] < 0)
            {
                momentum += state.Velocities[i] * state.Masses[i];
                mass += state.Masses[i];
            }
        }

        Vector3D drift = momentum * (1 / mass);
        for (int i = 0; i < state.Masses.Length; i++)
        {
            state.Velocities[i] -= drift;  // Realms' are worked out from their trees anyway.
        }
    }

    // Everything about the system at one step.
    private sealed class State(int count)
    {
        public double Time { get; set; }

        public long Step { get; set; }

        public Vector3D[] Positions { get; } = new Vector3D[count];

        public Vector3D[] Velocities { get; } = new Vector3D[count];

        public Vector3D[] Accelerations { get; set; } = new Vector3D[count];

        public double[] Masses { get; } = new double[count];

        public bool[] Alive { get; } = new bool[count];

        public State Clone()
        {
            var copy = new State(Masses.Length);
            CopyTo(copy);
            return copy;
        }

        public void CopyTo(State other)
        {
            other.Time = Time;
            other.Step = Step;
            Positions.CopyTo(other.Positions, 0);
            Velocities.CopyTo(other.Velocities, 0);
            other.Accelerations = (Vector3D[])Accelerations.Clone();
            Masses.CopyTo(other.Masses, 0);
            Alive.CopyTo(other.Alive, 0);
        }
    }
}
