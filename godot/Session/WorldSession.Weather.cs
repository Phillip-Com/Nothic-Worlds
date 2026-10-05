using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Measurement;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Session;

// The weather part of the open world (VISION.md WTH-01): weather pins and each body's average
// temperature, all undoable.
public partial class WorldSession
{
    /// <summary>Adds a named weather pin on a planet or moon.</summary>
    /// <returns>The new pin, or what's wrong (nothing is added then).</returns>
    public (WeatherPin? Pin, string? Problem) AddWeatherPin(
        Guid bodyId, GeoCoordinate spot, string name)
    {
        if (FindBody(bodyId) is not Body body || !body.HasSurface)
        {
            return (null, "weather pins go on planets and moons");
        }

        var pin = new WeatherPin { BodyId = bodyId, Name = name.Trim(), Spot = spot };
        if (pin.Problem() is string problem)
        {
            return (null, problem);
        }

        if (World.WeatherPins.Count >= WeatherPin.MaxPins)
        {
            return (null, $"a world can hold up to {WeatherPin.MaxPins:N0} weather pins");
        }

        RecordUndo("Add Weather Pin");
        World.WeatherPins.Add(pin);
        MarkChanged(systemChanged: false);
        return (pin, null);
    }

    /// <summary>Renames a weather pin.</summary>
    /// <returns>What's wrong with the name (nothing is changed then), or null.</returns>
    public string? RenameWeatherPin(Guid pinId, string name)
    {
        int index = World.WeatherPins.FindIndex(p => p.Id == pinId);
        if (index < 0)
        {
            return null;
        }

        WeatherPin renamed = World.WeatherPins[index] with { Name = name.Trim() };
        if (renamed.Problem() is string problem)
        {
            return problem;
        }

        if (renamed == World.WeatherPins[index])
        {
            return null;
        }

        RecordUndo($"Rename {World.WeatherPins[index].Name}");
        World.WeatherPins[index] = renamed;
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>Deletes a weather pin (Ctrl+Z brings it back).</summary>
    public void DeleteWeatherPin(Guid pinId)
    {
        int index = World.WeatherPins.FindIndex(p => p.Id == pinId);
        if (index < 0)
        {
            return;
        }

        RecordUndo($"Delete {World.WeatherPins[index].Name}");
        World.WeatherPins.RemoveAt(index);
        MarkChanged(systemChanged: false);
    }

    /// <summary>
    /// Sets a planet or moon's average temperature (VISION.md WTH-01). Rapid changes (holding
    /// the field's arrow) are one undo step.
    /// </summary>
    /// <returns>What's wrong with the value (nothing is changed then), or null.</returns>
    public string? SetAverageTemperature(Guid bodyId, double temperatureC)
    {
        if (FindBody(bodyId) is not Body body || body.AverageTemperatureC == temperatureC)
        {
            return null;
        }

        if (!double.IsFinite(temperatureC)
            || temperatureC is < Body.MinAverageTemperatureC or > Body.MaxAverageTemperatureC)
        {
            UnitSystem units = AppSettings.Units;
            return "an average temperature must be " +
                $"{Units.Format(Quantity.Temperature, Body.MinAverageTemperatureC, units)} to " +
                Units.Format(Quantity.Temperature, Body.MaxAverageTemperatureC, units);
        }

        RecordUndo($"Edit {body.Name}", mergeKey: ("temperature", bodyId));
        body.AverageTemperatureC = temperatureC;
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>
    /// Gives a planet or moon air, and so live weather, or takes it away (VISION.md WTH-02), as
    /// one undo step.
    /// </summary>
    public void SetAtmosphere(Guid bodyId, bool hasAtmosphere)
    {
        if (FindBody(bodyId) is not Body body || !body.HasSurface
            || body.HasAtmosphere == hasAtmosphere)
        {
            return;
        }

        RecordUndo(hasAtmosphere ? $"Give {body.Name} Air" : $"Take {body.Name}'s Air");
        body.HasAtmosphere = hasAtmosphere;
        MarkChanged(systemChanged: false);
    }
}
