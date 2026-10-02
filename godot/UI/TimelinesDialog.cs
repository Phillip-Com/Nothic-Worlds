using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Interop;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Manages the world's timelines (VISION.md LORE-03), the lanes of the timeline strip: add,
/// rename, recolor, show or hide, reorder, and delete them. Changes apply straight away, each
/// undoable. Deleting a timeline deletes its events too (owner's choice; Ctrl+Z brings them
/// back).
/// </summary>
public partial class TimelinesDialog : AcceptDialog
{
    private VBoxContainer _rows = null!;
    private Label _empty = null!;

    // The timelines the rows were built for, in order; rebuilt only when that changes.
    private string _signature = "";

    /// <summary>The open world. Set it before adding the dialog to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    public override void _Ready()
    {
        Title = "Timelines";
        OkButtonText = "Done";
        var layout = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
        _empty = new Label { Text = "No timelines yet. Each one is a lane on the strip." };
        layout.AddChild(_empty);
        _rows = new VBoxContainer();
        layout.AddChild(_rows);
        var add = new Button
        {
            Text = "Add Timeline",
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        add.Pressed += () => Session.AddTimeline();
        layout.AddChild(add);
        AddChild(layout);

        Session.Changed += Refresh;
    }

    public override void _ExitTree()
    {
        Session.Changed -= Refresh;
    }

    /// <summary>Opens the dialog.</summary>
    public void Open()
    {
        _signature = "";
        Refresh();
        PopupCentered();
    }

    private void Refresh()
    {
        if (!Visible && _signature != "")
        {
            return;
        }

        IReadOnlyList<Timeline> timelines = Session.World.Timelines;
        _empty.Visible = timelines.Count == 0;
        string signature = string.Join("|", timelines.Select(t => t.Id)) + "#";
        if (signature == _signature)
        {
            // Same lanes: just bring the fields up to date (undo may have changed them).
            foreach ((Node row, Timeline timeline) in _rows.GetChildren().Zip(timelines))
            {
                ShowValues((HBoxContainer)row, timeline);
            }

            return;
        }

        _signature = signature;
        foreach (Node row in _rows.GetChildren())
        {
            _rows.RemoveChild(row);
            row.QueueFree();
        }

        foreach (Timeline timeline in timelines)
        {
            _rows.AddChild(BuildRow(timeline));
        }

        ResetSize();
    }

    private HBoxContainer BuildRow(Timeline timeline)
    {
        Guid id = timeline.Id;
        Timeline Current() => Session.World.Timelines.First(t => t.Id == id);

        var row = new HBoxContainer();
        var color = new ColorPickerButton
        {
            CustomMinimumSize = new Vector2(32, 0),
            EditAlpha = false,
            TooltipText = "The lane's color",
        };
        color.ColorChanged += value =>
            Session.UpdateTimeline(Current() with { Color = value.ToRgbColor() });
        row.AddChild(color);

        var name = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        name.TextChanged += text =>
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                Session.UpdateTimeline(Current() with { Name = text.Trim() });
            }
        };
        row.AddChild(name);

        var shown = new CheckBox
        {
            Text = "Shown",
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Show this lane on the strip (hiding keeps its events)",
        };
        shown.Toggled += on => Session.UpdateTimeline(Current() with { Hidden = !on });
        row.AddChild(shown);

        row.AddChild(SmallButton("↑", "Move up", () => Session.MoveTimeline(id, -1)));
        row.AddChild(SmallButton("↓", "Move down", () => Session.MoveTimeline(id, +1)));
        row.AddChild(SmallButton("✕", "Delete this timeline and its events (Ctrl+Z undoes)",
            () => Session.DeleteTimeline(id)));
        ShowValues(row, timeline);
        return row;
    }

    // Puts a timeline's values in its row, leaving a name being typed alone.
    private static void ShowValues(HBoxContainer row, Timeline timeline)
    {
        var color = (ColorPickerButton)row.GetChild(0);
        var name = (LineEdit)row.GetChild(1);
        var shown = (CheckBox)row.GetChild(2);
        color.SetBlockSignals(true);
        shown.SetBlockSignals(true);
        color.Color = timeline.Color.ToGodot();
        shown.ButtonPressed = !timeline.Hidden;
        color.SetBlockSignals(false);
        shown.SetBlockSignals(false);
        if (!name.HasFocus() && name.Text != timeline.Name)
        {
            name.Text = timeline.Name;
        }
    }

    private static Button SmallButton(string text, string tooltip, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.None,
        };
        button.Pressed += pressed;
        return button;
    }
}
