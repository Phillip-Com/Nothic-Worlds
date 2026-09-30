using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Diagnostics;
using NothicWorlds.UI;

namespace NothicWorlds.Scenes;

/// <summary>
/// Root of the main scene. Sets up app-wide input actions, and handles command-line options
/// (given after <c>--</c>):
/// <list type="bullet">
/// <item>
/// <c>--map=&lt;path&gt;</c>: import a map image at startup, as if chosen with the button.
/// </item>
/// <item><c>--benchmark</c>: run the performance benchmark (after the map, if any, loads).</item>
/// </list>
/// </summary>
public partial class Main : Node3D
{
    private const string MapArgumentPrefix = "--map=";

    public override void _EnterTree()
    {
        // Runs before any child node is ready, so input actions exist before anything reads them.
        InputActions.Register();
    }

    public override async void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();

        string? mapArgument = arguments.FirstOrDefault(a => a.StartsWith(MapArgumentPrefix));
        if (mapArgument is not null)
        {
            // ImportAsync never throws; problems are shown in the toolbar's message line.
            string mapPath = mapArgument[MapArgumentPrefix.Length..];
            await GetNode<MapToolbar>("MapToolbar").ImportAsync(mapPath);
        }

        if (arguments.Contains(Benchmark.CommandLineFlag))
        {
            AddChild(new Benchmark { Camera = GetNode<PlanetCamera>("PlanetCamera") });
        }
    }
}
