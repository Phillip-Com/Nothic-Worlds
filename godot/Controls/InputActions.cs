using Godot;

namespace NothicWorlds.Controls;

/// <summary>
/// Names and default keys for every keyboard action in the app. Code refers to actions by these
/// names instead of raw keys, so keys can be made rebindable later in one place.
/// </summary>
public static class InputActions
{
    public const string CameraPanNorth = "camera_pan_north";
    public const string CameraPanSouth = "camera_pan_south";
    public const string CameraPanWest = "camera_pan_west";
    public const string CameraPanEast = "camera_pan_east";
    public const string CameraZoomIn = "camera_zoom_in";
    public const string CameraZoomOut = "camera_zoom_out";
    public const string CameraReset = "camera_reset";
    public const string TogglePerformanceOverlay = "toggle_performance_overlay";
    public const string ToggleGrid = "toggle_grid";
    public const string FileNew = "file_new";
    public const string FileOpen = "file_open";
    public const string FileSave = "file_save";
    public const string FileSaveAs = "file_save_as";

    /// <summary>
    /// Adds all actions to Godot's input map. Safe to call more than once. Must run before any
    /// node reads input (Main does this when it enters the scene tree).
    /// </summary>
    public static void Register()
    {
        Add(CameraPanNorth, Key.W, Key.Up);
        Add(CameraPanSouth, Key.S, Key.Down);
        Add(CameraPanWest, Key.A, Key.Left);
        Add(CameraPanEast, Key.D, Key.Right);
        Add(CameraZoomIn, Key.E, Key.Equal);
        Add(CameraZoomOut, Key.Q, Key.Minus);
        Add(CameraReset, Key.Home);
        Add(TogglePerformanceOverlay, Key.F3);
        Add(ToggleGrid, Key.G);
        AddShortcut(FileNew, Key.N);
        AddShortcut(FileOpen, Key.O);
        AddShortcut(FileSave, Key.S);
        AddShortcut(FileSaveAs, Key.S, shift: true);
    }

    // Ctrl (+ Shift) shortcuts. Unlike the camera keys, these follow the key's label, so Ctrl+S
    // is the key marked S on any keyboard layout. Check them with exactMatch: true, so Ctrl+S
    // doesn't also fire when Ctrl+Shift+S is pressed.
    private static void AddShortcut(string action, Key key, bool shift = false)
    {
        if (InputMap.HasAction(action))
        {
            return;
        }

        InputMap.AddAction(action);
        InputMap.ActionAddEvent(
            action, new InputEventKey { Keycode = key, CtrlPressed = true, ShiftPressed = shift });
    }

    private static void Add(string action, params Key[] keys)
    {
        if (InputMap.HasAction(action))
        {
            return;
        }

        InputMap.AddAction(action);
        foreach (Key key in keys)
        {
            // Physical keys, so WASD stays in the same place on non-QWERTY keyboards.
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
        }
    }
}
