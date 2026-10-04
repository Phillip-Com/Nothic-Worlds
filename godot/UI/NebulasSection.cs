using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Interop;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The System panel's nebulas (VISION.md BOD-03; owner's choice: a backdrop on the sky, the same
/// from every planet): each one's name, where it is on the sky, how big and bright it looks,
/// and its two colors, plus Add Nebula and Delete.
/// </summary>
public partial class NebulasSection : VBoxContainer
{
    private readonly List<NebulaRow> _rows = [];
    private VBoxContainer _list = null!;
    private Label _none = null!;
    private Button _add = null!;
    private bool _showing;

    /// <summary>The open world. Set it before adding the section to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    public override void _Ready()
    {
        AddChild(new Label { Text = "Nebulas" });
        _none = new Label
        {
            Text = "None on the sky yet.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_none);
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 10);
        AddChild(_list);
        _add = new Button
        {
            Text = "Add Nebula",
            FocusMode = FocusModeEnum.None,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            TooltipText = "A glowing cloud of gas on the sky, far beyond the system (the same " +
                "from every planet)",
        };
        _add.Pressed += () => Session.AddNebula();
        AddChild(_add);

        Session.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree() => Session.Changed -= Refresh;

    /// <summary>Shows the world's nebulas.</summary>
    public void Refresh()
    {
        if (!IsNodeReady())
        {
            return;
        }

        List<Nebula> nebulas = Session.World.Nebulas;
        _none.Visible = nebulas.Count == 0;
        _add.Disabled = nebulas.Count >= Nebula.MaxCount;
        if (!nebulas.Select(n => n.Id).SequenceEqual(_rows.Select(r => r.Id)))
        {
            foreach (NebulaRow row in _rows)
            {
                row.Root.QueueFree();
            }

            _rows.Clear();
            foreach (Nebula nebula in nebulas)
            {
                NebulaRow row = BuildRow(nebula.Id);
                _rows.Add(row);
                _list.AddChild(row.Root);
            }
        }

        _showing = true;
        foreach ((NebulaRow row, Nebula nebula) in _rows.Zip(nebulas))
        {
            if (!row.Name.HasFocus())
            {
                row.Name.Text = nebula.Name;
            }

            row.UpDown.SetValueNoSignal(nebula.LatitudeDegrees);
            row.Around.SetValueNoSignal(nebula.LongitudeDegrees);
            row.Size.SetValueNoSignal(nebula.SizeDegrees);
            row.Brightness.SetValueNoSignal(nebula.Brightness * 100);
            row.Color.Color = nebula.Color.ToGodot();
            row.SecondColor.Color = nebula.SecondColor.ToGodot();
        }

        _showing = false;
    }

    private NebulaRow BuildRow(Guid id)
    {
        var root = new VBoxContainer();
        var header = new HBoxContainer();
        var name = new LineEdit
        {
            PlaceholderText = "Nebula name",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        header.AddChild(name);
        var delete = new Button
        {
            Text = "Delete",
            FocusMode = FocusModeEnum.None,
            TooltipText = "Remove this nebula (Ctrl+Z brings it back)",
        };
        delete.Pressed += () => Session.RemoveNebula(id);
        header.AddChild(delete);
        root.AddChild(header);

        var grid = new GridContainer { Columns = 2 };
        root.AddChild(grid);
        var row = new NebulaRow(id, root, name,
            Field(grid, "Up/down", -90, 90, 1, "°",
                "How far above (or below) the system's plane it is on the sky"),
            Field(grid, "Around", 0, 360, 1, "°",
                "Which way around the sky it is, measured like orbit angles").WithWrapAround(),
            Field(grid, "Size", Nebula.MinSizeDegrees, Nebula.MaxSizeDegrees, 1, "°",
                "How big it looks: its radius on the sky"),
            Field(grid, "Brightness", Nebula.MinBrightness * 100, 100, 1, "%",
                "How strongly it glows"),
            ColorButton(grid, "Color", "Its main color"),
            ColorButton(grid, "Wisps", "The color its wisps blend toward"));

        name.TextSubmitted += _ => Commit(row);
        name.FocusExited += () => Commit(row);
        foreach (SpinBox field in new[] { row.UpDown, row.Around, row.Size, row.Brightness })
        {
            field.ValueChanged += _ => Commit(row);
        }

        row.Color.ColorChanged += _ => Commit(row);
        row.SecondColor.ColorChanged += _ => Commit(row);
        return row;
    }

    private void Commit(NebulaRow row)
    {
        if (_showing || Session.World.Nebulas.Find(n => n.Id == row.Id) is not Nebula nebula)
        {
            return;
        }

        string name = string.IsNullOrWhiteSpace(row.Name.Text)
            ? nebula.Name
            : row.Name.Text.Trim();
        Session.SetNebula(nebula with
        {
            Name = name.Length > Nebula.MaxNameLength ? name[..Nebula.MaxNameLength] : name,
            LatitudeDegrees = row.UpDown.Value,
            LongitudeDegrees = row.Around.Value % 360,
            SizeDegrees = row.Size.Value,
            Brightness = row.Brightness.Value / 100,
            Color = row.Color.Color.ToRgbColor(),
            SecondColor = row.SecondColor.Color.ToRgbColor(),
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

    private static ColorPickerButton ColorButton(GridContainer grid, string label, string tip)
    {
        grid.AddChild(new Label { Text = label });
        var button = new ColorPickerButton
        {
            EditAlpha = false,
            CustomMinimumSize = new Vector2(0, 28),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = tip,
        };
        grid.AddChild(button);
        return button;
    }

    // One nebula's controls.
    private sealed record NebulaRow(
        Guid Id,
        Control Root,
        LineEdit Name,
        SpinBox UpDown,
        SpinBox Around,
        SpinBox Size,
        SpinBox Brightness,
        ColorPickerButton Color,
        ColorPickerButton SecondColor);
}
