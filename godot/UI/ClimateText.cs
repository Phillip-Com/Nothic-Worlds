using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.UI;

/// <summary>
/// Words for terrain and the weather (VISION.md WTH-03): the climate kinds' names, and the
/// weather window's line saying how the painted terrain changes the weather.
/// </summary>
public static class ClimateText
{
    /// <summary>A climate kind's name, e.g. "Open land".</summary>
    public static string Name(ClimateKind kind) => kind switch
    {
        ClimateKind.Water => "Water",
        ClimateKind.Forest => "Forest",
        ClimateKind.Desert => "Desert",
        ClimateKind.Wetland => "Wetland",
        ClimateKind.Mountains => "Mountains",
        ClimateKind.Ice => "Ice",
        _ => "Open land",
    };

    /// <summary>What a climate kind does to the weather, for its tooltip.</summary>
    public static string Effect(ClimateKind kind) => kind switch
    {
        ClimateKind.Water => "Seas and lakes: milder, later seasons and a smaller day/night " +
            "swing nearby (within about 500 km)",
        ClimateKind.Forest => "A slightly smaller day/night swing",
        ClimateKind.Desert => "A much bigger day/night swing, and a little warmer",
        ClimateKind.Wetland => "A smaller day/night swing",
        ClimateKind.Mountains => "About 6 °C colder, with a bigger day/night swing",
        ClimateKind.Ice => "About 8 °C colder (ice reflects most sunlight)",
        _ => "No special effect on the weather",
    };

    /// <summary>
    /// The weather window's line about the terrain, e.g. "Terrain: Forest here, 40% water
    /// within 500 km: milder, later seasons and a smaller day/night swing; a slightly smaller
    /// day/night swing."
    /// </summary>
    public static string Describe(TerrainSurroundings? terrain)
    {
        if (terrain is null || (terrain.Here is null && terrain.WaterShare == 0))
        {
            return "Terrain: none painted here, so the weather is for plain ground.";
        }

        string here = terrain.Here is ClimateKind kind ? $"{Name(kind)} here" : "unpainted here";
        string water = $"{terrain.WaterShare:0%} water within {terrain.RadiusKm:0} km";
        var effects = new List<string>();
        if (terrain.Maritime >= 0.15)
        {
            effects.Add("milder, later seasons and a smaller day/night swing");
        }

        if (terrain.Here is ClimateKind ground and not ClimateKind.Water
            and not ClimateKind.OpenLand)
        {
            string effect = Effect(ground);
            effects.Add(char.ToLowerInvariant(effect[0]) + effect[1..]);  // Keeps "°C"
        }

        return effects.Count == 0
            ? $"Terrain: {here}, {water}: no noticeable effect."
            : $"Terrain: {here}, {water}: {string.Join("; ", effects)}.";
    }
}
