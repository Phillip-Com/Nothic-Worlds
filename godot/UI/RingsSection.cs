using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Interop;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The System panel's rings (VISION.md BOD-03; owner's choice: banded, in your color): switch
/// them on for the selected planet or moon, and set where they start and end (in the body's
/// radii) and their color.
/// </summary>
public partial class RingsSection : VBoxContainer
{
    private CheckBox _on = null!;
    private Control _fields = null!;
    private SpinBox _inner = null!;
    private SpinBox _outer = null!;
    private ColorPickerButton _color = null!;
    private bool _showing;

    /// <summary>The open world. Set it before adding the section to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    public override void _Ready()
    {
        _on = new CheckBox
        {
            Text = "Rings",
            FocusMode = FocusModeEnum.None,
            TooltipText = "Rings around the body's equator, like Saturn's",
        };
        _on.Toggled += _ => Commit();
        AddChild(_on);

        var grid = new GridContainer { Columns = 2 };
        _inner = Field(grid, "From", PlanetRings.MinInnerRadii, PlanetRings.MaxOuterRadii - 0.1,
            "Where the rings start, in the body's radii (a flat world's: its disc's). " +
            "Saturn's: about 1.25");
        _outer = Field(grid, "To", PlanetRings.MinInnerRadii + 0.1, PlanetRings.MaxOuterRadii,
            "Where the rings end, in the body's radii (a flat world's: its disc's). Saturn's " +
            "main rings: about 2.3");
        grid.AddChild(new Label { Text = "Color" });
        _color = new ColorPickerButton
        {
            EditAlpha = false,
            CustomMinimumSize = new Vector2(0, 28),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "The rings' color; their bands are lighter and darker shades of it",
        };
        _color.ColorChanged += _ => Commit();
        grid.AddChild(_color);
        _fields = grid;
        AddChild(grid);

        Session.Changed += Refresh;
        Session.SelectionChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        Session.Changed -= Refresh;
        Session.SelectionChanged -= Refresh;
    }

    /// <summary>Shows the selected body's rings.</summary>
    public void Refresh()
    {
        if (!IsNodeReady())
        {
            return;
        }

        PlanetRings? rings = Session.SelectedBody.Rings;
        _showing = true;
        _on.ButtonPressed = rings is not null;
        PlanetRings shown = rings ?? PlanetRings.Default;
        _inner.Value = shown.InnerRadii;
        _outer.Value = shown.OuterRadii;
        _color.Color = shown.Color.ToGodot();
        _fields.Visible = rings is not null;
        _showing = false;
    }

    private void Commit()
    {
        if (_showing)
        {
            return;
        }

        // The rings must end farther out than they start: moving one edge past the other
        // pushes the other along.
        double inner = _inner.Value;
        double outer = Math.Max(_outer.Value, inner + 0.1);
        Session.SetRings(Session.SelectedBodyId, _on.ButtonPressed
            ? new PlanetRings(inner, outer, _color.Color.ToRgbColor())
            : null);
    }

    private SpinBox Field(GridContainer grid, string label, double min, double max,
        string tooltip)
    {
        grid.AddChild(new Label { Text = label });
        var field = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = 0.05,
            Suffix = "× radius",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = tooltip,
        }.WithArrowKeys();
        field.ValueChanged += _ => Commit();
        grid.AddChild(field);
        return field;
    }
}
