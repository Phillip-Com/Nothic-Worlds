using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The System panel's asteroid belts for the selected star (VISION.md BOD-03; owner's choice:
/// one feature per belt): each belt's name, where it starts and ends (in AU), how thick and
/// dense it is, and its color, plus Add Belt and Delete.
/// </summary>
public partial class BeltsSection : VBoxContainer
{
    private const double KmPerAu = CometTail.KmPerAu;

    private readonly List<BeltRow> _rows = [];
    private VBoxContainer _list = null!;
    private Label _none = null!;
    private bool _showing;

    /// <summary>The open world. Set it before adding the section to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    public override void _Ready()
    {
        AddChild(new Label { Text = "Asteroid Belts" });
        _none = new Label
        {
            Text = "None yet.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_none);
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 10);
        AddChild(_list);
        var add = new Button
        {
            Text = "Add Belt",
            FocusMode = FocusModeEnum.None,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            TooltipText = "A belt of asteroids around this star, like our main belt (or just " +
                "beyond the outermost one)",
        };
        add.Pressed += () => Session.AddBelt(Session.SelectedBodyId);
        AddChild(add);

        Session.Changed += Refresh;
        Session.SelectionChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        Session.Changed -= Refresh;
        Session.SelectionChanged -= Refresh;
    }

    /// <summary>Shows the selected star's belts.</summary>
    public void Refresh()
    {
        if (!IsNodeReady())
        {
            return;
        }

        IReadOnlyList<AsteroidBelt> belts = Session.SelectedBody.Belts;
        _none.Visible = belts.Count == 0;
        if (!belts.Select(b => b.Id).SequenceEqual(_rows.Select(r => r.Id)))
        {
            foreach (BeltRow row in _rows)
            {
                row.Root.QueueFree();
            }

            _rows.Clear();
            foreach (AsteroidBelt belt in belts)
            {
                BeltRow row = BuildRow(belt.Id);
                _rows.Add(row);
                _list.AddChild(row.Root);
            }
        }

        _showing = true;
        foreach ((BeltRow row, AsteroidBelt belt) in _rows.Zip(belts))
        {
            if (!row.Name.HasFocus())
            {
                row.Name.Text = belt.Name;
            }

            row.Inner.SetValueNoSignal(belt.InnerKm / KmPerAu);
            row.Outer.SetValueNoSignal(belt.OuterKm / KmPerAu);
            row.Thickness.SetValueNoSignal(belt.ThicknessDegrees);
            row.Density.SetValueNoSignal(belt.Density * 100);
            row.Color.Color = belt.Color.ToGodot();
        }

        _showing = false;
    }

    private BeltRow BuildRow(Guid id)
    {
        var root = new VBoxContainer();
        var header = new HBoxContainer();
        var name = new LineEdit
        {
            PlaceholderText = "Belt name",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        header.AddChild(name);
        var delete = new Button
        {
            Text = "Delete",
            FocusMode = FocusModeEnum.None,
            TooltipText = "Remove this belt (Ctrl+Z brings it back)",
        };
        delete.Pressed += () => Session.RemoveBelt(Session.SelectedBodyId, id);
        header.AddChild(delete);
        root.AddChild(header);

        var grid = new GridContainer { Columns = 2 };
        root.AddChild(grid);
        var row = new BeltRow(id, root, name,
            Field(grid, "From", 0.001, 60_000, 0.01, "AU",
                "Where the belt starts, from the star (our main belt: about 2.2 AU)"),
            Field(grid, "To", 0.002, 60_000, 0.01, "AU",
                "Where the belt ends (our main belt: about 3.3 AU)"),
            Field(grid, "Thickness", 0, AsteroidBelt.MaxThicknessDegrees, 0.5, "°",
                "How far the rocks' orbits tilt, at most (0 is a flat belt)"),
            Field(grid, "Density", AsteroidBelt.MinDensity * 100, 100, 1, "%",
                "How crowded the belt is (more rocks drawn, and later more asteroid events)"),
            new ColorPickerButton
            {
                EditAlpha = false,
                CustomMinimumSize = new Vector2(0, 28),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = "The rocks' color",
            });
        grid.AddChild(new Label { Text = "Color" });
        grid.AddChild(row.Color);

        name.TextSubmitted += _ => Commit(row);
        name.FocusExited += () => Commit(row);
        foreach (SpinBox field in new[] { row.Inner, row.Outer, row.Thickness, row.Density })
        {
            field.ValueChanged += _ => Commit(row);
        }

        row.Color.ColorChanged += _ => Commit(row);
        return row;
    }

    // The belt as the row shows it. Moving one edge past the other pushes the other along.
    private void Commit(BeltRow row)
    {
        if (_showing || Session.SelectedBody.Belts.FirstOrDefault(b => b.Id == row.Id)
            is not AsteroidBelt belt)
        {
            return;
        }

        double inner = row.Inner.Value * KmPerAu;
        double outer = Math.Max(row.Outer.Value * KmPerAu, inner * 1.01);
        string name = string.IsNullOrWhiteSpace(row.Name.Text) ? belt.Name : row.Name.Text.Trim();
        Session.SetBelt(Session.SelectedBodyId, belt with
        {
            Name = name.Length > AsteroidBelt.MaxNameLength
                ? name[..AsteroidBelt.MaxNameLength]
                : name,
            InnerKm = inner,
            OuterKm = outer,
            ThicknessDegrees = row.Thickness.Value,
            Density = row.Density.Value / 100,
            Color = row.Color.Color.ToRgbColor(),
        });
    }

    private static SpinBox Field(GridContainer grid, string label, double min, double max,
        double step, string suffix, string tooltip)
    {
        grid.AddChild(new Label { Text = label });
        var field = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Suffix = suffix,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = tooltip,
        }.WithArrowKeys();
        grid.AddChild(field);
        return field;
    }

    // One belt's controls.
    private sealed record BeltRow(
        Guid Id,
        Control Root,
        LineEdit Name,
        SpinBox Inner,
        SpinBox Outer,
        SpinBox Thickness,
        SpinBox Density,
        ColorPickerButton Color);
}
