using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// The Tool On/Off switch at the top of the Terrain, Regions, and Map panels (owner's request,
/// 2026-10-08): while it's off, the panel's tool leaves drags on the globe alone, so they turn
/// the view. P flips it while its panel shows (a text field being typed in takes the key
/// first). Only one of these panels is open at a time, so P never reaches two.
/// </summary>
public partial class ToolSwitch : Button
{
    /// <summary>The hint shown while the tool is off.</summary>
    public const string OffHint = "The tool is off: drag the planet to turn the view. Press P " +
        "or click Tool Off to turn it back on.";

    private const string UsualTip =
        "Turn the tool off to drag the planet freely, and on again to use it (P)";

    /// <summary>Makes the switch, on.</summary>
    public ToolSwitch()
    {
        ToggleMode = true;
        ButtonPressed = true;
        FocusMode = FocusModeEnum.None;
        TooltipText = UsualTip;
        Text = TextFor(true);
        Toggled += on => Text = TextFor(on);
    }

    /// <summary>Whether the tool is on (and the switch isn't disabled).</summary>
    public bool IsOn => ButtonPressed && !Disabled;

    /// <summary>
    /// Enables the switch, or disables it with <paramref name="whyNot"/> as its tooltip.
    /// </summary>
    public void SetUnavailable(string? whyNot) => DisabledTip.Apply(this, UsualTip, whyNot);

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (IsVisibleInTree() && !Disabled
            && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.P })
        {
            ButtonPressed = !ButtonPressed;  // Raises Toggled, as a click does
            GetViewport().SetInputAsHandled();
        }
    }

    private static string TextFor(bool on) => on ? "Tool On (P)" : "Tool Off (P)";
}
