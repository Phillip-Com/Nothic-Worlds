namespace NothicWorlds.Core.Model;

/// <summary>
/// How a world is drawn (VISION.md REN-05; owner's choice: these three, chosen per world, with
/// Painterly the default). Only the look changes; the world itself is the same in every style.
/// </summary>
public enum VisualStyle
{
    /// <summary>Soft bands of light, brushy surfaces, and gentle outlines. The default.</summary>
    Painterly,

    /// <summary>True lighting, as the app drew worlds before styles.</summary>
    Realistic,

    /// <summary>Flat colors, a crisp day and night, and clean outlines.</summary>
    Simple,
}
