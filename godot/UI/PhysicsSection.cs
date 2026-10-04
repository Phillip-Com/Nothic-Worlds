using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The System panel's physics mode section (VISION.md SIM-03), shown while physics is on:
/// since when it's been running, the collisions so far (each with Go to), and Keep as orbits,
/// which makes the paths gravity has the bodies on now their designed orbits.
/// </summary>
public partial class PhysicsSection : VBoxContainer
{
    // The most collisions listed (the latest ones).
    private const int MaxRows = 12;

    private Label _status = null!;
    private VBoxContainer _rows = null!;

    // What the section shows, to rebuild it only when that changes.
    private string _signature = "";

    /// <summary>The open world. Set it before adding the section to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>The time bar, for gliding to a collision (no Go to buttons without it).</summary>
    public TimeControls? Time { get; init; }

    /// <summary>The message line, for what Keep as orbits couldn't keep.</summary>
    public MapToolbar? Toolbar { get; init; }

    public override void _Ready()
    {
        AddChild(new Label { Text = "Physics" });
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_status);
        _rows = new VBoxContainer();
        AddChild(_rows);
        var keep = new Button
        {
            Text = "Keep as Orbits",
            FocusMode = FocusModeEnum.None,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            TooltipText = "Make the paths gravity has the bodies on now their designed orbits " +
                "(Ctrl+Z undoes it), and switch physics off",
        };
        keep.Pressed += KeepOrbits;
        AddChild(keep);
        Visible = false;
    }

    public override void _Process(double delta)
    {
        // The simulation works on its own thread, so look for news each frame (cheaply).
        PhysicsMode physics = Session.Physics;
        PhysicsSnapshot? latest = physics.Latest;
        string signature = physics.IsOn
            ? $"{physics.StartDays}|{Session.SelectedBodyId}|{latest?.CaughtUp}|" +
                $"{latest?.Collisions.Count}"
            : "off";
        if (signature != _signature)
        {
            _signature = signature;
            Show(physics, latest);
        }
    }

    private void Show(PhysicsMode physics, PhysicsSnapshot? latest)
    {
        Visible = physics.IsOn;
        foreach (Node row in _rows.GetChildren())
        {
            _rows.RemoveChild(row);
            row.QueueFree();
        }

        if (!physics.IsOn)
        {
            return;
        }

        Body body = Session.SelectedBody;
        IReadOnlyList<Collision> collisions = latest?.Collisions ?? [];
        _status.Text = $"Real gravity since {BodyClock.Describe(body, physics.StartDays)}." +
            (latest is { CaughtUp: false } ? " Working out the jump…" : "") +
            (collisions.Count == 0 ? " No collisions yet." : "");
        foreach (Collision collision in collisions.TakeLast(MaxRows))
        {
            _rows.AddChild(CollisionRow(body, collision));
        }
    }

    private Control CollisionRow(Body body, Collision collision)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label
        {
            Text = $"{NameOf(collision.AbsorbedId)} merged into {NameOf(collision.IntoId)}\n" +
                BodyClock.Describe(body, collision.TimeDays),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        if (Time is TimeControls time)
        {
            var goTo = new Button
            {
                Text = "Go to",
                FocusMode = FocusModeEnum.None,
                TooltipText = "Run the clock to the collision",
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            double when = collision.TimeDays;
            goTo.Pressed += () => time.GlideTo(when);
            row.AddChild(goTo);
        }

        return row;
    }

    private void KeepOrbits()
    {
        if (Session.KeepPhysicsOrbits() is not { } notKept)
        {
            return;
        }

        if (notKept.Count == 0)
        {
            Toolbar?.ShowInfo("Kept the bodies' paths as their orbits.");
            return;
        }

        string skipped = string.Join("; ",
            notKept.Select(pair => $"{NameOf(pair.Key)} ({pair.Value})"));
        Toolbar?.ShowWarning(
            $"Kept the other paths as orbits. These keep their designs: {skipped}.");
    }

    private string NameOf(Guid bodyId) =>
        Session.World.Bodies.Find(b => b.Id == bodyId)?.Name ?? "A body";
}
