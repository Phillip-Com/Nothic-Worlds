using Godot;
using NothicWorlds.Core;

namespace NothicWorlds.Scenes;

/// <summary>
/// Root of the main scene. For now it only confirms that the Godot project is linked to the
/// core library.
/// </summary>
public partial class Main : Node3D
{
    public override void _Ready()
    {
        GD.Print($"{ProjectInfo.Name}: core library linked.");
    }
}
