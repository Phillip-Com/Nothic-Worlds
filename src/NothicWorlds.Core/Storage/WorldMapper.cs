using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Storage;

/// <summary>
/// Converts between the in-memory <see cref="World"/> and the file's
/// <see cref="WorldDocument"/>. Everything read from a file is validated here, because world
/// files are untrusted input (CLAUDE.md §4).
/// </summary>
internal static class WorldMapper
{
    public static WorldDocument ToDocument(World world)
    {
        return new WorldDocument
        {
            FormatVersion = WorldFormat.CurrentVersion,
            Id = world.Id,
            Name = world.Name,
            CreatedUtc = world.CreatedUtc,
            ModifiedUtc = world.ModifiedUtc,
            Bodies = world.Bodies.Select(ToDocument).ToList(),
            View = world.View is null ? null : ToDocument(world.View),
        };
    }

    /// <exception cref="WorldFileException">The document has missing or invalid data.</exception>
    public static World ToWorld(WorldDocument document)
    {
        Require(document.Bodies is { Count: > 0 }, "it has no planet");

        var world = new World
        {
            Id = document.Id,
            Name = RequireText(document.Name, "world name"),
            CreatedUtc = document.CreatedUtc,
            ModifiedUtc = document.ModifiedUtc,
            View = document.View is null ? null : ToView(document.View),
        };
        world.Bodies.AddRange(document.Bodies.Select(ToBody));
        return world;
    }

    private static BodyDocument ToDocument(Body body)
    {
        SurfaceMap? map = body.Surface.Map;
        return new BodyDocument
        {
            Id = body.Id,
            Name = body.Name,
            Kind = WorldFormat.BodyKindName(body.Kind),
            Surface = new SurfaceDocument
            {
                Map = map is null ? null : new MapDocument
                {
                    Asset = map.AssetName,
                    Projection = WorldFormat.ProjectionName(map.Projection),
                },
                FillColor = body.Surface.FillColor.ToHex(),
            },
        };
    }

    private static ViewDocument ToDocument(CameraView view)
    {
        return new ViewDocument
        {
            Latitude = view.LatitudeDegrees,
            Longitude = view.LongitudeDegrees,
            Altitude = view.Altitude,
            FocusOffset = [view.FocusOffsetX, view.FocusOffsetY, view.FocusOffsetZ],
        };
    }

    private static Body ToBody(BodyDocument document)
    {
        Require(document is not null, "a planet entry is empty");
        Require(document!.Surface is not null, "a planet has no surface data");
        Require(
            RgbColor.TryParseHex(document.Surface!.FillColor, out RgbColor fillColor),
            $"invalid fill color '{document.Surface.FillColor}'");

        var body = new Body
        {
            Id = document.Id,
            Name = RequireText(document.Name, "planet name"),
            Kind = WorldFormat.ParseBodyKind(document.Kind),
        };
        body.Surface.FillColor = fillColor;

        if (document.Surface.Map is MapDocument map)
        {
            Require(
                WorldFormat.IsValidAssetName(map.Asset), $"invalid map image name '{map.Asset}'");
            body.Surface.Map = new SurfaceMap
            {
                AssetName = map.Asset,
                Projection = WorldFormat.ParseProjection(map.Projection),
            };
        }

        return body;
    }

    private static CameraView ToView(ViewDocument document)
    {
        double[] offset = document.FocusOffset ?? [0, 0, 0];
        Require(offset.Length == 3, "the saved camera view is invalid");
        double[] numbers =
            [document.Latitude, document.Longitude, document.Altitude, .. offset];
        Require(numbers.All(double.IsFinite), "the saved camera view is invalid");

        return new CameraView(
            document.Latitude, document.Longitude, document.Altitude,
            offset[0], offset[1], offset[2]);
    }

    private static string RequireText(string? text, string what)
    {
        Require(!string.IsNullOrWhiteSpace(text), $"the {what} is missing");
        return text!;
    }

    private static void Require(bool condition, string problem)
    {
        if (!condition)
        {
            throw new WorldFileException($"The world data is damaged: {problem}.");
        }
    }
}
