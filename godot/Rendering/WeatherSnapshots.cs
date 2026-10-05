using Godot;

namespace NothicWorlds.Rendering;

/// <summary>
/// The two snapshots of one body's live weather that its globe blends between (VISION.md
/// WTH-02): an older and a newer, with when each is of. <see cref="WeatherDisplay"/> makes them;
/// <see cref="PlanetSurface.ShowWeather"/> draws them.
/// </summary>
public sealed class WeatherSnapshots
{
    private ImageTexture? _weatherOld, _weatherNew, _windOld, _windNew;

    /// <summary>When the older snapshot is of, in standard days (NaN before any).</summary>
    public double OldTime { get; private set; } = double.NaN;

    /// <summary>When the newer snapshot is of, in standard days (NaN before any).</summary>
    public double NewTime { get; private set; } = double.NaN;

    /// <summary>True once a snapshot has arrived.</summary>
    public bool HasWeather => !double.IsNaN(NewTime);

    /// <summary>
    /// Takes a new snapshot of the weather at <paramref name="timeDays"/>. Unless
    /// <paramref name="alone"/>, the last one becomes the older, to blend from; alone (after the
    /// clock jumps) it stands for both.
    /// </summary>
    public void Add(Image weather, Image wind, double timeDays, bool alone)
    {
        if (alone || !HasWeather)
        {
            _weatherOld = Fill(_weatherOld, weather);
            _windOld = Fill(_windOld, wind);
            OldTime = timeDays;
        }
        else
        {
            // There's a newer snapshot already (HasWeather), which becomes the older.
            (_weatherOld, _weatherNew) = (_weatherNew!, _weatherOld);
            (_windOld, _windNew) = (_windNew!, _windOld);
            OldTime = NewTime;
        }

        _weatherNew = Fill(_weatherNew, weather);
        _windNew = Fill(_windNew, wind);
        NewTime = timeDays;
    }

    /// <summary>
    /// Shows the snapshots on a globe at a time between them (or at the nearer one).
    /// </summary>
    public void ShowOn(PlanetSurface surface, double timeDays)
    {
        if (_weatherOld is null || _weatherNew is null || _windOld is null || _windNew is null)
        {
            return;
        }

        double span = NewTime - OldTime;
        float blend = span > 0 ? (float)Math.Clamp((timeDays - OldTime) / span, 0, 1) : 1f;
        surface.ShowWeather(_weatherOld, _weatherNew, _windOld, _windNew, blend, timeDays);
    }

    // Puts an image into a texture, making a new one if there's none or the size changed.
    private static ImageTexture Fill(ImageTexture? texture, Image image)
    {
        if (texture is not null && texture.GetSize() == image.GetSize()
            && texture.GetFormat() == image.GetFormat())
        {
            texture.Update(image);
            return texture;
        }

        return ImageTexture.CreateFromImage(image);
    }
}
