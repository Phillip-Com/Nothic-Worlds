using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.UI;

/// <summary>
/// Words for eclipses (VISION.md EVT-01), shared by the System panel and the orbit markers.
/// </summary>
public static class EclipseText
{
    /// <summary>
    /// What an eclipse is, from the selected body's point of view: "Total solar eclipse
    /// (Moon)", "Partial lunar eclipse of Moon", or, for the moon itself, "Total lunar eclipse
    /// (in Earth's shadow)".
    /// </summary>
    public static string Title(Eclipse eclipse, Body selected, IReadOnlyList<Body> bodies)
    {
        string kind = $"{Short(eclipse)} eclipse";
        string NameOf(Guid id) => bodies.FirstOrDefault(b => b.Id == id)?.Name ?? "?";
        if (eclipse.Kind == EclipseKind.Solar)
        {
            return $"{kind} ({NameOf(eclipse.BlockerId)})";
        }

        return eclipse.ShadowedId == selected.Id
            ? $"{kind} (in {NameOf(eclipse.BlockerId)}'s shadow)"
            : $"{kind} of {NameOf(eclipse.ShadowedId)}";
    }

    /// <summary>A short name for labels on the orbit: "Total solar", "Penumbral lunar".</summary>
    public static string Short(Eclipse eclipse)
    {
        string kind = eclipse.Kind == EclipseKind.Solar ? "solar" : "lunar";
        return $"{eclipse.Type} {kind}";
    }

    /// <summary>
    /// How deep and how long: "94% of the star hidden · 5.8 h", "outer shadow only · 4.4 h".
    /// </summary>
    public static string Details(Eclipse eclipse)
    {
        string depth = eclipse switch
        {
            { Kind: EclipseKind.Solar } => $"{eclipse.Coverage:0%} of the star hidden",
            { Type: EclipseType.Penumbral } => "outer shadow only",
            _ => $"{eclipse.Coverage:0%} in full shadow",
        };
        double hours = (eclipse.EndDays - eclipse.StartDays) * 24;
        return $"{depth} · {hours:0.0} h";
    }
}
