using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Storage;
using NothicWorlds.UI;

namespace NothicWorlds.Session;

/// <summary>
/// The crash safety net (VISION.md SAV-02). While a world has unsaved changes, a recovery copy is
/// written quietly in the background every few minutes. If the app didn't close normally, the
/// next start offers to recover it. Copies are deleted once the world is saved, or once the user
/// chooses not to keep the changes.
/// </summary>
public partial class RecoveryService : Node
{
    /// <summary>How often a recovery copy is written while there are unsaved changes.</summary>
    [Export] public double IntervalSeconds { get; set; } = 300.0;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>Where recovery results are reported.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    private RecoveryStore _store = null!;
    private bool _isWriting;
    private bool _wasUnsaved;

    public override void _Ready()
    {
        if (Session is null)
        {
            GD.PushError("RecoveryService needs a world session.");
            return;
        }

        _store = new RecoveryStore(
            Path.Combine(ProjectSettings.GlobalizePath("user://"), "recovery"));

        // A saved world, or one whose changes the user chose to discard, no longer needs a copy.
        Session.Saved += _store.Delete;
        Session.WorldClosed += _store.Delete;
        Session.Changed += DeleteCopyIfBackToSaved;

        var timer = new Godot.Timer { WaitTime = IntervalSeconds, Autostart = true };
        timer.Timeout += () => _ = WriteCopyAsync();
        AddChild(timer);

        // After the rest of the app is ready, so dialogs and the message line exist.
        Callable.From(OfferRecovery).CallDeferred();
    }

    // Undoing back to exactly what's saved makes the copy pointless (and a crash would then
    // offer to "recover" what's already in the file).
    private void DeleteCopyIfBackToSaved()
    {
        bool unsaved = Session!.HasUnsavedChanges;
        if (_wasUnsaved && !unsaved)
        {
            _store.Delete(Session.World.Id);
        }

        _wasUnsaved = unsaved;
    }

    private async Task WriteCopyAsync()
    {
        if (Session is not { HasUnsavedChanges: true, IsBusy: false } || _isWriting)
        {
            return;
        }

        World snapshot = Session.World.Clone();
        snapshot.View = Session.Camera?.GetView();
        var assets = new Dictionary<string, IAssetSource>(Session.Assets);
        string? originalPath = Session.FilePath;

        _isWriting = true;
        try
        {
            await Task.Run(() => _store.Save(snapshot, assets, originalPath));
            GD.Print($"Recovery copy written for “{snapshot.Name}”.");
        }
        catch (Exception error)
        {
            // Never interrupt the user over a failed safety copy; the next attempt may work.
            GD.PushWarning($"Couldn't write a recovery copy: {error.Message}");
        }
        finally
        {
            _isWriting = false;
        }
    }

    // Offers the newest recovery copy, if the app didn't close normally last time. Older ones
    // (rare: several crashes in a row) are offered on later starts, so nothing is dropped.
    private void OfferRecovery()
    {
        if (_store.Find() is not [RecoveryEntry entry, ..])
        {
            return;
        }

        string where = entry.OriginalPath is null
            ? "a world that was never saved"
            : $"“{Path.GetFileNameWithoutExtension(entry.OriginalPath)}”";
        var dialog = new ConfirmationDialog
        {
            Title = "Recover Unsaved Changes?",
            DialogText =
                "Nothic Worlds didn't close normally last time.\n" +
                $"Recover the unsaved changes to {where} from " +
                $"{entry.SavedUtc.ToLocalTime():g}?",
            OkButtonText = "Recover",
            CancelButtonText = "Discard",
        };
        dialog.Confirmed += () => _ = RecoverAsync(entry);
        dialog.Canceled += () => _store.Delete(entry.WorldId);
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private async Task RecoverAsync(RecoveryEntry entry)
    {
        try
        {
            string? warning = await Session!.RecoverAsync(entry);
            string message = "Recovered your unsaved changes. Save (Ctrl+S) to keep them.";
            if (warning is null)
            {
                Toolbar?.ShowInfo(message, autoHide: false);
            }
            else
            {
                Toolbar?.ShowWarning($"{message} {warning}");
            }
        }
        catch (WorldFileException error)
        {
            // Keep the copy (never delete user data on a failure); it's offered again next time.
            Toolbar?.ShowError($"Couldn't recover the unsaved changes: {error.Message}");
        }
    }
}
