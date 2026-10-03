using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// A dropdown (<see cref="OptionButton"/>) that opens when the click ends rather than when it
/// starts. In a short window the list can open over the button itself, and the end of the same
/// click would otherwise pick whatever item is under the mouse (found with real clicks at
/// 1152 × 648). Use it for every dropdown.
/// </summary>
public partial class Dropdown : OptionButton
{
    public Dropdown()
    {
        ActionMode = ActionModeEnum.Release;
    }
}
