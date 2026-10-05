namespace NothicWorlds.Maps;

/// <summary>
/// Whether imported maps are kept uncompressed (VISION.md REN-03; owner's choice: the Advanced
/// tier's high-quality map textures, in File ▸ Settings). Compressed, an 8k map uses about
/// 137 MB of video memory with slight blockiness in fine detail; uncompressed, about 497 MB and
/// perfect. Read as each map or piece is prepared, so changing it applies to those loaded after.
/// </summary>
public static class MapQuality
{
    /// <summary>True to keep maps and map pieces uncompressed.</summary>
    public static bool Uncompressed { get; set; }
}
