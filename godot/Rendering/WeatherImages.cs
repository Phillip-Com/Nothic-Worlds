using Godot;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Rendering;

/// <summary>
/// Snapshots of live weather as images for <c>clouds.gdshader</c> (VISION.md WTH-02): one spot
/// per pixel, running east from longitude −180° and south from the north pole. Safe to make on
/// another thread; only turning them into textures needs the main one.
/// </summary>
public static class WeatherImages
{
    // Rain and snow are stored on a log scale up to this many mm an hour.
    private const double HeaviestMmPerHour = 20.0;

    // Winds are stored from −this to +this, in m/s.
    private const double WindRangeMs = 40.0;

    /// <summary>
    /// The weather image (red cloud cover, green rain, blue snow) and the wind image (red east,
    /// green north) of a moment, <paramref name="width"/> spots around and half that from pole
    /// to pole.
    /// </summary>
    public static (Image Weather, Image Wind) Of(WeatherMoment moment, int width)
    {
        int height = width / 2;
        byte[] weather = new byte[width * height * 4];
        byte[] wind = new byte[width * height * 2];
        Parallel.For(0, height, row =>
        {
            double latitude = 90 - (row + 0.5) * 180 / height;
            for (int column = 0; column < width; column++)
            {
                double longitude = -180 + (column + 0.5) * 360 / width;
                WeatherSample sample = moment.SampleAt(
                    new Core.Geometry.GeoCoordinate(latitude, longitude));
                int at = row * width + column;
                double falling = Math.Log(1 + sample.PrecipitationMmPerHour)
                    / Math.Log(1 + HeaviestMmPerHour);
                bool snow = sample.Precipitation == PrecipitationKind.Snow;
                weather[at * 4] = ToByte(sample.CloudCover);
                weather[at * 4 + 1] = ToByte(snow ? 0 : falling);
                weather[at * 4 + 2] = ToByte(snow ? falling : 0);
                weather[at * 4 + 3] = 255;
                wind[at * 2] = ToByte(0.5 + sample.WindEastMs / (2 * WindRangeMs));
                wind[at * 2 + 1] = ToByte(0.5 + sample.WindNorthMs / (2 * WindRangeMs));
            }
        });

        return (Image.CreateFromData(width, height, false, Image.Format.Rgba8, weather),
            Image.CreateFromData(width, height, false, Image.Format.Rg8, wind));
    }

    private static byte ToByte(double value) =>
        (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
}
