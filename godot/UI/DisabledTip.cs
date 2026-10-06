using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// Turns a button on or off so that, while it's off, its tooltip says why and what to do
/// (VISION.md UI-05: a tool never just does nothing without saying why).
/// </summary>
public static class DisabledTip
{
    /// <summary>Why a button waits while the world is saving, opening, or loading a map.</summary>
    public const string Busy = "Wait for the world to finish saving or loading";

    /// <summary>Why a button needs the selected body's main map.</summary>
    public const string NoMap = "There's no map yet: Import Map… first";

    /// <summary>Why a button needs a planet or moon selected.</summary>
    public const string NoSurface =
        "Stars and comets have no surface: select a planet or moon first";

    /// <summary>
    /// Enables <paramref name="button"/> with its usual <paramref name="tip"/>, or, when
    /// <paramref name="whyNot"/> is given, disables it with that reason as its tooltip.
    /// </summary>
    public static void Apply(BaseButton button, string tip, string? whyNot)
    {
        button.Disabled = whyNot is not null;
        button.TooltipText = whyNot ?? tip;
    }
}
