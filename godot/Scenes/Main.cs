using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Diagnostics;

namespace NothicWorlds.Scenes;

/// <summary>
/// Root of the main scene. Sets up app-wide input actions and, when requested on the command
/// line, the performance benchmark.
/// </summary>
public partial class Main : Node3D
{
    public override void _EnterTree()
    {
        // Runs before any child node is ready, so input actions exist before anything reads them.
        InputActions.Register();
    }

    public override void _Ready()
    {
        if (OS.GetCmdlineUserArgs().Contains(Benchmark.CommandLineFlag))
        {
            AddChild(new Benchmark { Camera = GetNode<PlanetCamera>("PlanetCamera") });
        }
    }
}
