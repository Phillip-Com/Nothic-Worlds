namespace NothicWorlds.Core.Geometry;

/// <summary>
/// Where plants stand on a square of ground in the standing view (VISION.md REN-06): spots
/// strewn evenly at random over a <see cref="GroundTile"/>, the same every time for the same
/// body, layer, and tile, so plants stay put as the eye moves and come back where they were.
/// What grows at each spot is chosen by its <see cref="ScatterPoint.Pick"/>
/// (<see cref="Model.PlantMix.Choose"/>).
/// </summary>
public static class PlantScatter
{
    /// <summary>
    /// The spots on a tile, about <paramref name="expected"/> of them (the whole number below
    /// it, and one more as often as its fraction), for a body's <paramref name="seed"/> and a
    /// <paramref name="layer"/> (so layers don't stand on the same spots).
    /// </summary>
    public static ScatterPoint[] Points(int seed, int layer, GroundTile tile, double expected)
    {
        if (!double.IsFinite(expected) || expected <= 0)
        {
            return [];
        }

        var random = new SplitMix(Key(seed, layer, tile));
        int count = (int)Math.Floor(expected);
        if (random.Next() < expected - count)
        {
            count++;
        }

        var points = new ScatterPoint[count];
        for (int i = 0; i < count; i++)
        {
            points[i] = new ScatterPoint(random.Next(), random.Next(), random.Next(),
                random.Next(), random.Next(), random.Next());
        }

        return points;
    }

    // One number for everything that picks a tile's spots, well mixed.
    private static ulong Key(int seed, int layer, GroundTile tile)
    {
        ulong key = (uint)seed;
        foreach (long part in (ReadOnlySpan<long>)[layer, tile.Root, tile.Level, tile.X, tile.Y])
        {
            key = SplitMix.Mix(key ^ (ulong)part);
        }

        return key;
    }

    // A small, fast generator of numbers from 0 to 1 (SplitMix64), the same on every machine.
    private struct SplitMix(ulong state)
    {
        private ulong _state = state;

        public double Next()
        {
            _state += 0x9E3779B97F4A7C15;
            return (Mix(_state) >> 11) * (1.0 / (1UL << 53));
        }

        public static ulong Mix(ulong value)
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EB;
            return value ^ (value >> 31);
        }
    }
}
