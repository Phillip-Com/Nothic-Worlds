using System.IO.Compression;
using System.Text;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Storage;

namespace NothicWorlds.Core.Tests.Storage;

public sealed class WorldPackageTests : IDisposable
{
    private const string AssetName = "assets/0123456789abcdef0123456789abcdef.png";

    // A version 1 world file, exactly as the first release wrote it. It must load forever
    // (upgraded on load). Never edit this.
    private const string GoldenV1Json = """
        {
          "formatVersion": 1,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "bodies": [
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel"
                },
                "fillColor": "#112233"
              }
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    // A version 2 world file (adds map calibration), exactly as this version writes it. If this
    // test fails, the file format changed: that must be deliberate, with a new format version,
    // a migration, and a new golden file. Never edit this.
    private const string GoldenV2Json = """
        {
          "formatVersion": 2,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "bodies": [
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "fillColor": "#112233"
              }
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private static readonly byte[] _imageBytes = Encoding.ASCII.GetBytes("pretend PNG bytes");

    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "nothic-worlds-tests", Guid.NewGuid().ToString("N"));

    public WorldPackageTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    // ----- Round trips -----

    [Fact]
    public void SaveThenLoad_PreservesEverything()
    {
        World original = GoldenWorld();
        string path = PathFor("aerth.nworld");

        WorldPackage.Save(path, original, AssetsFromImage());
        LoadedWorld loaded = WorldPackage.Load(path);

        AssertSameWorld(original, loaded.World);
        Assert.Equal(_imageBytes, ReadAsset(loaded, AssetName));
    }

    [Fact]
    public void SaveThenLoad_WorldWithoutMapOrView()
    {
        World original = World.CreateNew("Blank");
        string path = PathFor("blank.nworld");

        WorldPackage.Save(path, original, new Dictionary<string, IAssetSource>());
        LoadedWorld loaded = WorldPackage.Load(path);

        AssertSameWorld(original, loaded.World);
        Assert.Empty(loaded.Assets);
    }

    [Theory]
    [InlineData(MapProjection.Equirectangular)]
    [InlineData(MapProjection.Mercator)]
    [InlineData(MapProjection.Robinson)]
    [InlineData(MapProjection.WinkelTripel)]
    [InlineData(MapProjection.Mollweide)]
    [InlineData(MapProjection.GallPeters)]
    [InlineData(MapProjection.Polar)]
    [InlineData(MapProjection.TwoHemispheres)]
    public void SaveThenLoad_EveryMapType(MapProjection projection)
    {
        World original = GoldenWorld();
        original.Bodies[0].Surface.Map!.Projection = projection;
        string path = PathFor("types.nworld");

        WorldPackage.Save(path, original, AssetsFromImage());

        Assert.Equal(projection, WorldPackage.Load(path).World.Bodies[0].Surface.Map!.Projection);
    }

    [Fact]
    public void ResavingALoadedWorld_KeepsItsAssets()
    {
        string path = PathFor("resave.nworld");
        WorldPackage.Save(path, GoldenWorld(), AssetsFromImage());

        // Assets now come from the file being overwritten.
        LoadedWorld loaded = WorldPackage.Load(path);
        loaded.World.Name = "Renamed";
        WorldPackage.Save(path, loaded.World, loaded.Assets);

        LoadedWorld reloaded = WorldPackage.Load(path);
        Assert.Equal("Renamed", reloaded.World.Name);
        Assert.Equal(_imageBytes, ReadAsset(reloaded, AssetName));
    }

    // ----- The file format itself -----

    [Fact]
    public void WrittenJson_MatchesTheGoldenVersion2File()
    {
        string path = PathFor("golden.nworld");

        WorldPackage.Save(path, CalibratedGoldenWorld(), AssetsFromImage());

        Assert.Equal(Normalize(GoldenV2Json), Normalize(ReadEntry(path, "world.json")));
    }

    [Fact]
    public void GoldenVersion2File_LoadsAsExpected()
    {
        string path = WriteRawPackage(
            "golden-v2.nworld", GoldenV2Json, (AssetName, _imageBytes));

        AssertSameWorld(CalibratedGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void GoldenVersion1File_StillLoads_WithoutCalibration()
    {
        string path = WriteRawPackage(
            "golden-v1.nworld", GoldenV1Json, (AssetName, _imageBytes));

        World world = WorldPackage.Load(path).World;

        AssertSameWorld(GoldenWorld(), world);
        Assert.Null(world.Bodies[0].Surface.Map!.Calibration);
    }

    [Fact]
    public void ResavingAVersion1File_WritesTheCurrentVersion()
    {
        string oldPath = WriteRawPackage("old.nworld", GoldenV1Json, (AssetName, _imageBytes));
        LoadedWorld loaded = WorldPackage.Load(oldPath);
        string newPath = PathFor("upgraded.nworld");

        WorldPackage.Save(newPath, loaded.World, loaded.Assets);

        Assert.Contains("\"formatVersion\": 2", ReadEntry(newPath, "world.json"));
        AssertSameWorld(GoldenWorld(), WorldPackage.Load(newPath).World);
    }

    [Fact]
    public void Calibration_SurvivesARoundTrip()
    {
        World original = CalibratedGoldenWorld();
        MapCalibration calibration = original.Bodies[0].Surface.Map!.Calibration!;
        string path = PathFor("calibrated.nworld");

        WorldPackage.Save(path, original, AssetsFromImage());
        MapCalibration loaded =
            WorldPackage.Load(path).World.Bodies[0].Surface.Map!.Calibration!;

        foreach (double lat in new[] { -80.0, -12.0, 30.0, 47.0 })
        {
            Assert.Equal(calibration.DrawnLatitude(lat), loaded.DrawnLatitude(lat), 1e-12);
        }

        foreach (double lon in new[] { -179.0, -45.0, 0.0, 179.0 })
        {
            Assert.Equal(calibration.DrawnLongitude(lon), loaded.DrawnLongitude(lon), 1e-12);
        }
    }

    [Fact]
    public void Save_StoresTheOriginalImageUnchangedAndDropsUnusedAssets()
    {
        World world = GoldenWorld();
        var assets = new Dictionary<string, IAssetSource>(AssetsFromImage())
        {
            ["assets/ffffffffffffffffffffffffffffffff.png"] = new BytesAssetSource([1, 2, 3]),
        };
        string path = PathFor("unused.nworld");

        WorldPackage.Save(path, world, assets);

        using ZipArchive archive = ZipFile.OpenRead(path);
        Assert.Equal(
            ["assets/0123456789abcdef0123456789abcdef.png", "world.json"],
            archive.Entries.Select(entry => entry.FullName).Order());
    }

    // ----- Safe saving -----

    [Fact]
    public void SavingOverAWorld_KeepsTheOldVersionAsABackup()
    {
        string path = PathFor("backup.nworld");
        WorldPackage.Save(path, World.CreateNew("First"), new Dictionary<string, IAssetSource>());

        WorldPackage.Save(path, World.CreateNew("Second"), new Dictionary<string, IAssetSource>());

        Assert.Equal("Second", WorldPackage.Load(path).World.Name);
        Assert.Equal("First", WorldPackage.Load(path + WorldPackage.BackupSuffix).World.Name);
    }

    [Fact]
    public void FailedSave_LeavesTheExistingWorldUntouched()
    {
        string path = PathFor("safe.nworld");
        WorldPackage.Save(path, World.CreateNew("Precious"), NoAssets());
        byte[] before = File.ReadAllBytes(path);

        // The image can't be read partway through saving.
        var failing = new Dictionary<string, IAssetSource>
        {
            [AssetName] = new FailingAssetSource(),
        };
        Assert.Throws<WorldFileException>(() => WorldPackage.Save(path, GoldenWorld(), failing));

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFiles(_folder));  // No leftover temp file.
    }

    [Fact]
    public void Save_MissingAsset_FailsBeforeTouchingDisk()
    {
        string path = PathFor("missing-asset.nworld");

        WorldFileException error = Assert.Throws<WorldFileException>(
            () => WorldPackage.Save(path, GoldenWorld(), new Dictionary<string, IAssetSource>()));

        Assert.Contains("missing", error.Message);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void Save_ToAMissingFolder_ExplainsInPlainLanguage()
    {
        string path = Path.Combine(_folder, "no-such-folder", "world.nworld");

        WorldFileException error = Assert.Throws<WorldFileException>(
            () => WorldPackage.Save(path, World.CreateNew(), NoAssets()));

        Assert.Equal("Couldn't save the world: the folder doesn't exist.", error.Message);
    }

    // ----- Rejecting bad or future files -----

    [Fact]
    public void Load_NewerFormatVersion_IsRefusedWithAClearMessage()
    {
        string path = WriteRawPackage(
            "future.nworld", GoldenV2Json.Replace("\"formatVersion\": 2", "\"formatVersion\": 3"),
            (AssetName, _imageBytes));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("newer version", error.Message);
    }

    [Theory]
    [InlineData("\"formatVersion\": 1,", "")]                               // No version
    [InlineData("\"projection\": \"winkel-tripel\"", "\"projection\": \"cubist\"")]
    [InlineData("\"kind\": \"planet\"", "\"kind\": \"teapot\"")]
    [InlineData("\"fillColor\": \"#112233\"", "\"fillColor\": \"blue\"")]
    [InlineData("\"name\": \"Aerth\",\n  \"createdUtc\"", "\"name\": \"\",\n  \"createdUtc\"")]
    [InlineData("0123456789abcdef0123456789abcdef.png\"", "../../evil.png\"")]  // Path escape
    [InlineData("\"altitude\": 1.25", "\"altitude\": \"high\"")]
    public void Load_DamagedWorldData_IsRejected(string find, string replace)
    {
        string json = Normalize(GoldenV1Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV1Json), json);  // The edit really applied.
        string path = WriteRawPackage("damaged.nworld", json, (AssetName, _imageBytes));

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Theory]
    [InlineData("\"drawnAs\": 33.5", "\"drawnAs\": 95")]      // Latitude drawn past the pole
    [InlineData("\"drawnAs\": 2", "\"drawnAs\": 300")]        // Longitude moved half a turn
    [InlineData("\"drawnAs\": -185", "\"drawnAs\": 10")]      // Longitudes out of order
    [InlineData("\"drawnAs\": 33.5", "\"drawn\": 33.5")]      // Missing value
    public void Load_DamagedCalibration_IsRejected(string find, string replace)
    {
        string json = Normalize(GoldenV2Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV2Json), json);  // The edit really applied.
        string path = WriteRawPackage("damaged-v2.nworld", json, (AssetName, _imageBytes));

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Fact]
    public void Load_MapImageMissingFromFile_IsRejected()
    {
        string path = WriteRawPackage("no-image.nworld", GoldenV1Json);

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("missing", error.Message);
    }

    [Fact]
    public void Load_NotAZipFile_IsRejected()
    {
        string path = PathFor("text.nworld");
        File.WriteAllText(path, "just some text");

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Fact]
    public void Load_ZipWithoutWorldData_IsRejected()
    {
        string path = PathFor("other.zip");
        using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            archive.CreateEntry("readme.txt");
        }

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("isn't a Nothic Worlds world file", error.Message);
    }

    [Fact]
    public void Load_MissingFile_IsRejected()
    {
        Assert.Throws<WorldFileException>(() => WorldPackage.Load(PathFor("nope.nworld")));
    }

    [Fact]
    public void CreateAssetName_IsUniqueAndValid()
    {
        string first = WorldPackage.CreateAssetName(".PNG");
        string second = WorldPackage.CreateAssetName("png");

        Assert.NotEqual(first, second);
        Assert.Matches("^assets/[0-9a-f]{32}\\.png$", first);
        Assert.Throws<ArgumentException>(() => WorldPackage.CreateAssetName(".exe"));
    }

    // ----- Helpers -----

    private static World GoldenWorld()
    {
        var world = new World
        {
            Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Name = "Aerth",
            CreatedUtc = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero),
            ModifiedUtc = new DateTimeOffset(2026, 9, 30, 13, 30, 0, TimeSpan.Zero),
            View = new CameraView(20, -45.5, 1.25, 0.5, 0, -0.25),
        };
        var planet = new Body
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Name = "Aerth",
            Kind = BodyKind.Planet,
        };
        planet.Surface.Map = new SurfaceMap
        {
            AssetName = AssetName,
            Projection = MapProjection.WinkelTripel,
        };
        planet.Surface.FillColor = new RgbColor(0x11, 0x22, 0x33);
        world.Bodies.Add(planet);
        return world;
    }

    // The golden world with the calibration in the version 2 golden file.
    private static World CalibratedGoldenWorld()
    {
        World world = GoldenWorld();
        world.Bodies[0].Surface.Map!.Calibration = MapCalibration.Create(
            [new CalibrationGuide(30, 33.5)],
            [new CalibrationGuide(-180, -185), new CalibrationGuide(0, 2)]);
        return world;
    }

    private static void AssertSameWorld(World expected, World actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.CreatedUtc, actual.CreatedUtc);
        Assert.Equal(expected.ModifiedUtc, actual.ModifiedUtc);
        Assert.Equal(expected.View, actual.View);
        Assert.Equal(expected.Bodies.Count, actual.Bodies.Count);
        for (int i = 0; i < expected.Bodies.Count; i++)
        {
            Body e = expected.Bodies[i];
            Body a = actual.Bodies[i];
            Assert.Equal(e.Id, a.Id);
            Assert.Equal(e.Name, a.Name);
            Assert.Equal(e.Kind, a.Kind);
            Assert.Equal(e.Surface.FillColor, a.Surface.FillColor);
            Assert.Equal(e.Surface.Map?.AssetName, a.Surface.Map?.AssetName);
            Assert.Equal(e.Surface.Map?.Projection, a.Surface.Map?.Projection);
            Assert.Equal(
                e.Surface.Map?.Calibration?.Latitudes, a.Surface.Map?.Calibration?.Latitudes);
            Assert.Equal(
                e.Surface.Map?.Calibration?.Longitudes, a.Surface.Map?.Calibration?.Longitudes);
        }
    }

    private static Dictionary<string, IAssetSource> AssetsFromImage()
    {
        return new Dictionary<string, IAssetSource>
        {
            [AssetName] = new BytesAssetSource(_imageBytes),
        };
    }

    private static Dictionary<string, IAssetSource> NoAssets()
    {
        return [];
    }

    private static byte[] ReadAsset(LoadedWorld world, string name)
    {
        using Stream stream = world.Assets[name].OpenRead();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static string ReadEntry(string packagePath, string entryName)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        using var reader = new StreamReader(archive.GetEntry(entryName)!.Open());
        return reader.ReadToEnd();
    }

    // Builds a world file by hand, for testing files this code didn't write.
    private string WriteRawPackage(
        string fileName, string json, params (string Name, byte[] Bytes)[] assets)
    {
        string path = PathFor(fileName);
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var writer = new StreamWriter(archive.CreateEntry("world.json").Open()))
        {
            writer.Write(json);
        }

        foreach ((string name, byte[] bytes) in assets)
        {
            using Stream stream = archive.CreateEntry(name).Open();
            stream.Write(bytes);
        }

        return path;
    }

    private string PathFor(string fileName)
    {
        return Path.Combine(_folder, fileName);
    }

    private static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n").Trim();
    }

    private sealed class BytesAssetSource(byte[] bytes) : IAssetSource
    {
        public Stream OpenRead() => new MemoryStream(bytes, writable: false);
    }

    private sealed class FailingAssetSource : IAssetSource
    {
        public Stream OpenRead() => throw new IOException("Simulated disk error.");
    }
}
