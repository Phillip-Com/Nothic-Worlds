using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Storage;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The File menu (VISION.md SAV-01, SAV-02): New, Open, Save, and Save As, with Ctrl+N / Ctrl+O /
/// Ctrl+S / Ctrl+Shift+S. It asks before discarding unsaved changes, and keeps the window title
/// showing the world's name, with a • when there are unsaved changes. New worlds are saved to
/// <c>Documents\Nothic Worlds\</c> by default.
/// </summary>
public partial class FileMenu : Node
{
    private const string AppName = "Nothic Worlds";

    private MenuButton _button = null!;
    private FileDialog _openDialog = null!;
    private FileDialog _saveDialog = null!;
    private ConfirmationDialog _unsavedDialog = null!;
    private TaskCompletionSource<string?>? _pendingPath;
    private TaskCompletionSource<UnsavedChoice>? _pendingChoice;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>
    /// The toolbar: the menu goes in its row, and messages go in its message line.
    /// </summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>Where world files go unless the user picks somewhere else.</summary>
    public static string DefaultFolder => Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
        AppName);

    private enum MenuItem
    {
        New,
        Open,
        Save,
        SaveAs,
    }

    private enum UnsavedChoice
    {
        Save,
        Discard,
        Cancel,
    }

    public override void _Ready()
    {
        if (Session is null || Toolbar is null)
        {
            GD.PushError("FileMenu needs a world session and a toolbar.");
            return;
        }

        _button = new MenuButton
        {
            Text = "File",
            Flat = false,
            FocusMode = Control.FocusModeEnum.None,
        };
        PopupMenu menu = _button.GetPopup();
        // The accelerators show the shortcut beside each item. Whether the menu or
        // _UnhandledInput catches the key press, marking it handled stops it firing twice.
        menu.AddItem("New World", (int)MenuItem.New, Ctrl(Key.N));
        menu.AddItem("Open…", (int)MenuItem.Open, Ctrl(Key.O));
        menu.AddSeparator();
        menu.AddItem("Save", (int)MenuItem.Save, Ctrl(Key.S));
        menu.AddItem("Save As…", (int)MenuItem.SaveAs, Ctrl(Key.S, shift: true));
        menu.IdPressed += id => Run((MenuItem)(int)id);
        Toolbar.MenuArea.AddChild(_button);

        _openDialog = CreateFileDialog("Open World", FileDialog.FileModeEnum.OpenFile);
        _saveDialog = CreateFileDialog("Save World As", FileDialog.FileModeEnum.SaveFile);
        _unsavedDialog = CreateUnsavedDialog();

        Session.Changed += UpdateTitle;
        UpdateTitle();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        MenuItem? item =
            IsShortcut(@event, InputActions.FileNew) ? MenuItem.New
            : IsShortcut(@event, InputActions.FileOpen) ? MenuItem.Open
            : IsShortcut(@event, InputActions.FileSaveAs) ? MenuItem.SaveAs
            : IsShortcut(@event, InputActions.FileSave) ? MenuItem.Save
            : null;
        if (item is MenuItem chosen)
        {
            GetViewport().SetInputAsHandled();
            Run(chosen);
        }
    }

    /// <summary>
    /// If there are unsaved changes, asks whether to save them first. Returns true if it's fine
    /// to continue (saved, or the user chose not to save); false if the user cancelled.
    /// </summary>
    /// <param name="action">What's about to happen, e.g. "closing".</param>
    public async Task<bool> ConfirmUnsavedChangesAsync(string action)
    {
        if (Session is not { HasUnsavedChanges: true })
        {
            return true;
        }

        _unsavedDialog.DialogText =
            $"Save changes to “{Session.World.Name}” before {action}?\n" +
            "If you don't save, your changes will be lost.";
        _pendingChoice = new TaskCompletionSource<UnsavedChoice>();
        _unsavedDialog.PopupCentered();

        return await _pendingChoice.Task switch
        {
            UnsavedChoice.Save => await SaveAsync(),
            UnsavedChoice.Discard => true,
            _ => false,
        };
    }

    /// <summary>Opens a world file, showing the outcome in the message line.</summary>
    public async Task OpenPathAsync(string path)
    {
        if (Session is null || Toolbar is null)
        {
            return;
        }

        string fileName = Path.GetFileName(path);
        Toolbar.ShowInfo($"Opening {fileName}…", autoHide: false);
        try
        {
            string? warning = await Session.OpenAsync(path);
            if (warning is null)
            {
                Toolbar.ShowInfo($"Opened “{Session.World.Name}”.");
            }
            else
            {
                Toolbar.ShowWarning($"Opened “{Session.World.Name}”. {warning}");
            }
        }
        catch (WorldFileException error)
        {
            Toolbar.ShowError($"Couldn't open {fileName}: {error.Message}");
        }
    }

    private async void Run(MenuItem item)
    {
        if (Session is null || Session.IsBusy)
        {
            Toolbar?.ShowInfo("Please wait until the current save or open finishes.");
            return;
        }

        switch (item)
        {
            case MenuItem.New:
                if (await ConfirmUnsavedChangesAsync("starting a new world"))
                {
                    Session.NewWorld();
                    Toolbar?.ShowInfo("Started a new world.");
                }

                break;
            case MenuItem.Open:
                if (await ConfirmUnsavedChangesAsync("opening another world")
                    && await AskForPathAsync(_openDialog, null) is string openPath)
                {
                    await OpenPathAsync(openPath);
                }

                break;
            case MenuItem.Save:
                await SaveAsync();
                break;
            case MenuItem.SaveAs:
                await SaveAsAsync();
                break;
        }
    }

    // Saves to the world's file, or asks where if it has never been saved.
    private async Task<bool> SaveAsync()
    {
        return Session?.FilePath is string path ? await SaveToAsync(path) : await SaveAsAsync();
    }

    private async Task<bool> SaveAsAsync()
    {
        string suggestedName = Session!.World.Name + WorldPackage.Extension;
        return await AskForPathAsync(_saveDialog, suggestedName) is string path
            && await SaveToAsync(path);
    }

    private async Task<bool> SaveToAsync(string path)
    {
        if (!path.EndsWith(WorldPackage.Extension, StringComparison.OrdinalIgnoreCase))
        {
            path += WorldPackage.Extension;
        }

        Toolbar?.ShowInfo($"Saving {Path.GetFileName(path)}…", autoHide: false);
        try
        {
            await Session!.SaveAsync(path);
            Toolbar?.ShowInfo($"Saved “{Session.World.Name}”.");
            return true;
        }
        catch (WorldFileException error)
        {
            Toolbar?.ShowError(error.Message);
            return false;
        }
    }

    // Shows a file dialog and waits for the user's choice (null if they cancel).
    private async Task<string?> AskForPathAsync(FileDialog dialog, string? suggestedName)
    {
        Directory.CreateDirectory(DefaultFolder);
        dialog.CurrentDir = Session?.FilePath is string current
            ? Path.GetDirectoryName(current)
            : DefaultFolder;
        if (suggestedName is not null)
        {
            dialog.CurrentFile = suggestedName;
        }

        _pendingPath = new TaskCompletionSource<string?>();
        dialog.PopupCentered();
        return await _pendingPath.Task;
    }

    private void UpdateTitle()
    {
        if (Session is null)
        {
            return;
        }

        string unsaved = Session.HasUnsavedChanges ? " •" : "";
        GetWindow().Title = $"{Session.World.Name}{unsaved} — {AppName}";
    }

    private FileDialog CreateFileDialog(string title, FileDialog.FileModeEnum mode)
    {
        var dialog = new FileDialog
        {
            Title = title,
            FileMode = mode,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
            Filters = [$"*{WorldPackage.Extension} ; {AppName} worlds"],
        };
        dialog.FileSelected += path => _pendingPath?.TrySetResult(path);
        dialog.Canceled += () => _pendingPath?.TrySetResult(null);
        AddChild(dialog);
        return dialog;
    }

    private ConfirmationDialog CreateUnsavedDialog()
    {
        var dialog = new ConfirmationDialog
        {
            Title = "Unsaved Changes",
            OkButtonText = "Save",
            CancelButtonText = "Cancel",
        };
        dialog.AddButton("Don't Save", right: false, action: "discard");
        dialog.Confirmed += () => _pendingChoice?.TrySetResult(UnsavedChoice.Save);
        dialog.Canceled += () => _pendingChoice?.TrySetResult(UnsavedChoice.Cancel);
        dialog.CustomAction += action =>
        {
            if (action == "discard")
            {
                dialog.Hide();
                _pendingChoice?.TrySetResult(UnsavedChoice.Discard);
            }
        };
        AddChild(dialog);
        return dialog;
    }

    // A Ctrl (+ Shift) key combination, as used for menu accelerators.
    private static Key Ctrl(Key key, bool shift = false)
    {
        long modifiers = (long)KeyModifierMask.MaskCtrl
            | (shift ? (long)KeyModifierMask.MaskShift : 0);
        return (Key)(modifiers | (long)key);
    }

    private static bool IsShortcut(InputEvent @event, string action)
    {
        // Exact match, so Ctrl+Shift+S isn't also taken as Ctrl+S.
        return @event.IsActionPressed(action, allowEcho: false, exactMatch: true);
    }
}
