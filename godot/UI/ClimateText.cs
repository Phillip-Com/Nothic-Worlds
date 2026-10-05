using NothicWorlds.Core.Measurement;
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

    // A drop in temperature of so many °C, in the chosen units ("6 °C" or "11 °F").
    private static string Colder(double celsius) =>
        UnitText.Format(Quantity.TemperatureChange, celsius);

    /// <summary>What a climate kind does to the weather, for its tooltip.</summary>
    public static string Effect(ClimateKind kind) => kind switch
    {
        ClimateKind.Water => "Seas and lakes: milder, later seasons, a smaller day/night " +
            $"swing, and more rain nearby (within about {UnitText.Format(Quantity.Distance, 500)})",
        ClimateKind.Forest => "A slightly smaller day/night swing, and a little wetter",
        ClimateKind.Desert => "A much bigger day/night swing, a little warmer, and very dry",
        ClimateKind.Wetland => "A smaller day/night swing, and wetter",
        ClimateKind.Mountains => $"About {Colder(6)} colder, with a bigger day/night swing " +
            "and more rain",
        ClimateKind.Ice => $"About {Colder(8)} colder (ice reflects most sunlight), and drier",
        _ => "No special effect on the weather",
    };

    // What the ground at a spot does to its temperatures, for the weather window's line (rain
    // is said separately there).
    private static string TemperatureEffect(ClimateKind kind) => kind switch
    {
        ClimateKind.Forest => "a slightly smaller day/night swing",
        ClimateKind.Desert => "a much bigger day/night swing, a little warmer",
        ClimateKind.Wetland => "a smaller day/night swing",
        ClimateKind.Mountains => $"about {Colder(6)} colder, a bigger day/night swing",
        ClimateKind.Ice => $"about {Colder(8)} colder",
        _ => "",
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
        string water = $"{terrain.WaterShare:0%} water within " +
            UnitText.Format(Quantity.Distance, terrain.RadiusKm);
        var effects = new List<string>();
        if (terrain.Maritime >= 0.15)
        {
            effects.Add("milder, later seasons and a smaller day/night swing");
        }

        if (terrain.Here is ClimateKind ground and not ClimateKind.Water
            and not ClimateKind.OpenLand)
        {
            effects.Add(TemperatureEffect(ground));
        }

        double moisture = ClimateYear.Moisture(terrain);
        if (moisture >= 1.15 || moisture <= 0.85)
        {
            effects.Add($"{(moisture > 1 ? "wetter" : "drier")} than average " +
                $"({moisture:0.#}× the rain)");
        }

        return effects.Count == 0
            ? $"Terrain: {here}, {water}: no noticeable effect."
            : $"Terrain: {here}, {water}: {string.Join("; ", effects)}.";
    }
}
