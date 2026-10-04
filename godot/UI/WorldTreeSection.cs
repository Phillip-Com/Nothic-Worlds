using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Interop;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The System panel's settings for a world tree (VISION.md BOD-02; owner's choice: grown from
/// settings, and glowing): how many great branches it has and how far they spread, its shape
/// number (each grows a different tree), its bark and leaf colors, and its glow.
/// </summary>
public partial class WorldTreeSection : VBoxContainer
{
    private SpinBox _branches = null!;
    private SpinBox _spread = null!;
    private SpinBox _seed = null!;
    private ColorPickerButton _bark = null!;
    private ColorPickerButton _leaves = null!;
    private ColorPickerButton _glow = null!;
    private SpinBox _glowStrength = null!;
    private bool _showing;

    /// <summary>The open world. Set it before adding the section to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    public override void _Ready()
    {
        AddChild(new Label { Text = "World Tree" });
        var grid = new GridContainer { Columns = 2 };
        AddChild(grid);
        _branches = Field(grid, "Branches", WorldTreeLook.MinBranches, WorldTreeLook.MaxBranches,
            1, "", "How many great branches it has; each can hold a realm");
        _spread = Field(grid, "Spread", WorldTreeLook.MinSpread * 100,
            WorldTreeLook.MaxSpread * 100, 5, "%", "How far its branches reach");
        _seed = Field(grid, "Shape", 0, 9999, 1, "",
            "Each number grows a differently shaped tree");
        _bark = ColorButton(grid, "Bark", "Its trunk and branches' color");
        _leaves = ColorButton(grid, "Leaves", "Its foliage's color");
        _glow = ColorButton(grid, "Glow", "The color of the light it gives off");
        _glowStrength = Field(grid, "Glow strength", 0, WorldTreeLook.MaxGlowStrength * 100, 5,
            "%", "How brightly it glows (0 for a dark tree)");

        Session.Changed += Refresh;
        Session.SelectionChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        Session.Changed -= Refresh;
        Session.SelectionChanged -= Refresh;
    }

    /// <summary>Shows the selected tree's settings.</summary>
    public void Refresh()
    {
        if (!IsNodeReady() || Session.SelectedBody.Tree is not WorldTreeLook look)
        {
            return;
        }

        _showing = true;
        _branches.SetValueNoSignal(look.Branches);
        _spread.SetValueNoSignal(look.Spread * 100);
        _seed.SetValueNoSignal(look.Seed);
        _bark.Color = look.Bark.ToGodot();
        _leaves.Color = look.Leaves.ToGodot();
        _glow.Color = look.Glow.ToGodot();
        _glowStrength.SetValueNoSignal(look.GlowStrength * 100);
        _showing = false;
    }

    private void Commit()
    {
        if (_showing || Session.SelectedBody.Tree is not WorldTreeLook look)
        {
            return;
        }

        Session.SetTreeLook(Session.SelectedBodyId, look with
        {
            Branches = (int)_branches.Value,
            Spread = _spread.Value / 100,
            Seed = (int)_seed.Value,
            Bark = _bark.Color.ToRgbColor(),
            Leaves = _leaves.Color.ToRgbColor(),
            Glow = _glow.Color.ToRgbColor(),
            GlowStrength = _glowStrength.Value / 100,
        });
    }

    private SpinBox Field(GridContainer grid, string label, double min, double max, double step,
        string suffix, string tooltip)
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
        field.ValueChanged += _ => Commit();
        grid.AddChild(field);
        return field;
    }

    private ColorPickerButton ColorButton(GridContainer grid, string label, string tooltip)
    {
        grid.AddChild(new Label { Text = label });
        var button = new ColorPickerButton
        {
            EditAlpha = false,
            CustomMinimumSize = new Vector2(0, 28),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = tooltip,
        };
        button.ColorChanged += _ => Commit();
        grid.AddChild(button);
        return button;
    }
}
