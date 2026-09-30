using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Diagnostics;
using NothicWorlds.Session;
using NothicWorlds.UI;

namespace NothicWorlds.Scenes;

/// <summary>
/// Root of the main scene. It sets up app-wide input actions, asks about unsaved changes before
/// the window closes, and handles command-line options (given after <c>--</c>):
/// <list type="bullet">
/// <item><c>--open=&lt;path&gt;</c>: open a world file at startup.</item>
/// <item>
/// <c>--map=&lt;path&gt;</c>: import a map image at startup, as if chosen with the button.
/// </item>
/// <item><c>--benchmark</c>: run the performance benchmark (after the map, if any, loads).</item>
/// </list>
/// </summary>
public partial class Main : Node3D
{
    private const string OpenArgumentPrefix = "--open=";
    private const string MapArgumentPrefix = "--map=";

    private bool _isQuitting;

    public override void _EnterTree()
    {
        // Runs before any child node is ready, so input actions exist before anything reads them.
        InputActions.Register();
    }

    public override async void _Ready()
    {
        // Closing the window asks about unsaved changes first (see _Notification).
        GetTree().AutoAcceptQuit = false;

        string[] arguments = OS.GetCmdlineUserArgs();

        if (ArgumentValue(arguments, OpenArgumentPrefix) is string worldPath)
        {
            await GetNode<FileMenu>("FileMenu").OpenPathAsync(worldPath);
        }

        if (ArgumentValue(arguments, MapArgumentPrefix) is string mapPath)
        {
            // ImportAsync never throws; problems are shown in the toolbar's message line.
            await GetNode<MapToolbar>("MapToolbar").ImportAsync(mapPath);
        }

        if (arguments.Contains(Benchmark.CommandLineFlag))
        {
            AddChild(new Benchmark { Camera = GetNode<PlanetCamera>("PlanetCamera") });
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            _ = QuitAsync();
        }
    }

    private async Task QuitAsync()
    {
        if (_isQuitting)
        {
            return;
        }

        _isQuitting = true;
        if (await GetNode<FileMenu>("FileMenu").ConfirmUnsavedChangesAsync("closing"))
        {
            GetNode<WorldSession>("WorldSession").Close();
            GetTree().Quit();
            return;
        }

        _isQuitting = false;
    }

    private static string? ArgumentValue(string[] arguments, string prefix)
    {
        return arguments.FirstOrDefault(a => a.StartsWith(prefix))?[prefix.Length..];
    }
}
