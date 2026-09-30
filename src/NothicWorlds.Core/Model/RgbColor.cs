using System.Globalization;

namespace NothicWorlds.Core.Model;

/// <summary>
/// An opaque color. Stored in world files as a hex code like <c>#E6EDF5</c>, which is readable
/// and familiar.
/// </summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    /// <summary>Returns the color as an uppercase hex code, e.g. <c>#E6EDF5</c>.</summary>
    public string ToHex()
    {
        return $"#{R:X2}{G:X2}{B:X2}";
    }

    /// <summary>
    /// Parses a hex code in the form <c>#RRGGBB</c> (case-insensitive). Returns false for
    /// anything else.
    /// </summary>
    public static bool TryParseHex(string? text, out RgbColor color)
    {
        color = default;
        if (text is not { Length: 7 } || text[0] != '#'
            || !int.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, null, out int value))
        {
            return false;
        }

        color = new RgbColor((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }
}
