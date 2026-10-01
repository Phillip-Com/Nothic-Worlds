using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The Edit menu: Undo and Redo, labelled with what they'd do (e.g. "Undo Move Piece 1"), with
/// Ctrl+Z and Ctrl+Y / Ctrl+Shift+Z. It sits beside the File menu, and the outcome shows in the
/// toolbar's message line.
/// </summary>
public partial class EditMenu : Node
{
    private MenuButton _button = null!;

    /// <summary>The open world, whose edits are undone.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The toolbar: the menu goes in its row, and messages in its message line.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    private enum MenuItem
    {
        Undo,
        Redo,
    }

    public override void _Ready()
    {
        if (Session is null || Toolbar is null)
        {
            GD.PushError("EditMenu needs a world session and a toolbar.");
            return;
        }

        _button = new MenuButton
        {
            Text = "Edit",
            Flat = false,
            FocusMode = Control.FocusModeEnum.None,
        };
        PopupMenu menu = _button.GetPopup();
        menu.AddItem("Undo", (int)MenuItem.Undo, Ctrl(Key.Z));
        menu.AddItem("Redo", (int)MenuItem.Redo, Ctrl(Key.Y));
        menu.IdPressed += id => _ = RunAsync((MenuItem)(int)id);

        // The labels are refreshed when the menu opens, so they always match what would happen.
        menu.AboutToPopup += UpdateItems;
        Toolbar.MenuArea.AddChild(_button);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Echo allowed: holding Ctrl+Z keeps undoing, as in most apps.
        MenuItem? item =
            @event.IsActionPressed(InputActions.EditUndo, allowEcho: true, exactMatch: true)
                ? MenuItem.Undo
            : @event.IsActionPressed(InputActions.EditRedo, allowEcho: true, exactMatch: true)
                ? MenuItem.Redo
            : null;
        if (item is MenuItem chosen)
        {
            GetViewport().SetInputAsHandled();
            _ = RunAsync(chosen);
        }
    }

    private void UpdateItems()
    {
        PopupMenu menu = _button.GetPopup();
        SetItem(menu, MenuItem.Undo, "Undo", Session!.UndoDescription, Session.CanUndo);
        SetItem(menu, MenuItem.Redo, "Redo", Session.RedoDescription, Session.CanRedo);
    }

    private static void SetItem(
        PopupMenu menu, MenuItem item, string verb, string? what, bool enabled)
    {
        int index = menu.GetItemIndex((int)item);
        menu.SetItemText(index, what is null ? verb : $"{verb} {what}");
        menu.SetItemDisabled(index, !enabled);
    }

    // Never throws; problems are shown in the toolbar.
    private async Task RunAsync(MenuItem item)
    {
        if (Session is null)
        {
            return;
        }

        bool undo = item == MenuItem.Undo;
        if (!(undo ? Session.CanUndo : Session.CanRedo))
        {
            bool nothing = undo ? Session.UndoDescription is null : Session.RedoDescription is null;
            Toolbar?.ShowInfo(nothing
                ? $"Nothing to {(undo ? "undo" : "redo")}."
                : "Finish what you're doing first, then try again.");
            return;
        }

        try
        {
            (string description, string? warning) =
                await (undo ? Session.UndoAsync() : Session.RedoAsync());
            string message = $"{(undo ? "Undid" : "Redid")} {description}.";
            if (warning is null)
            {
                Toolbar?.ShowInfo(message);
            }
            else
            {
                Toolbar?.ShowWarning($"{message} {warning}");
            }
        }
        catch (Exception error)
        {
            GD.PushError($"Unexpected error during {item}: {error}");
            Toolbar?.ShowError(
                $"Couldn't {(undo ? "undo" : "redo")} because of an unexpected error. " +
                "Details are in the log.");
        }
    }

    private static Key Ctrl(Key key)
    {
        return (Key)((long)KeyModifierMask.MaskCtrl | (long)key);
    }
}
