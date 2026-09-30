using NothicWorlds.Core.Model;
using NothicWorlds.Core.Storage;

namespace NothicWorlds.Core.Tests.Storage;

public sealed class RecoveryStoreTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "nothic-worlds-tests", Guid.NewGuid().ToString("N"));

    private readonly RecoveryStore _store;

    public RecoveryStoreTests()
    {
        _store = new RecoveryStore(Path.Combine(_folder, "recovery"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Find_WhenNothingWasSaved_IsEmpty()
    {
        Assert.Empty(_store.Find());
    }

    [Fact]
    public void Save_ThenFind_ReturnsARecoverableCopy()
    {
        World world = World.CreateNew("Unsaved work");
        world.Bodies[0].Surface.FillColor = new RgbColor(1, 2, 3);

        _store.Save(world, NoAssets(), originalPath: @"C:\Worlds\aerth.nworld");

        RecoveryEntry entry = Assert.Single(_store.Find());
        Assert.Equal(world.Id, entry.WorldId);
        Assert.Equal(@"C:\Worlds\aerth.nworld", entry.OriginalPath);
        World recovered = WorldPackage.Load(entry.RecoveryPath).World;
        Assert.Equal("Unsaved work", recovered.Name);
        Assert.Equal(new RgbColor(1, 2, 3), recovered.Bodies[0].Surface.FillColor);
    }

    [Fact]
    public void Save_NeverSavedWorld_HasNoOriginalPath()
    {
        _store.Save(World.CreateNew(), NoAssets(), originalPath: null);

        Assert.Null(Assert.Single(_store.Find()).OriginalPath);
    }

    [Fact]
    public void SavingAgain_ReplacesTheCopyWithoutBackups()
    {
        World world = World.CreateNew("First");
        _store.Save(world, NoAssets(), null);
        world.Name = "Second";

        _store.Save(world, NoAssets(), null);

        RecoveryEntry entry = Assert.Single(_store.Find());
        Assert.Equal("Second", WorldPackage.Load(entry.RecoveryPath).World.Name);
        Assert.False(File.Exists(entry.RecoveryPath + WorldPackage.BackupSuffix));
    }

    [Fact]
    public void Delete_RemovesEverythingForThatWorldOnly()
    {
        World keep = World.CreateNew("Keep");
        World remove = World.CreateNew("Remove");
        _store.Save(keep, NoAssets(), null);
        _store.Save(remove, NoAssets(), null);

        _store.Delete(remove.Id);

        Assert.Equal(keep.Id, Assert.Single(_store.Find()).WorldId);
        Assert.Equal(2, Directory.GetFiles(_store.Folder).Length);  // Keep's world + note
    }

    [Fact]
    public void Delete_WhenNothingExists_DoesNothing()
    {
        _store.Delete(Guid.NewGuid());

        Assert.Empty(_store.Find());
    }

    [Fact]
    public void Find_DamagedNote_StillOffersTheWorld()
    {
        World world = World.CreateNew();
        _store.Save(world, NoAssets(), @"C:\Worlds\x.nworld");
        File.WriteAllText(Path.Combine(_store.Folder, $"{world.Id:N}.json"), "{ not json");

        RecoveryEntry entry = Assert.Single(_store.Find());

        Assert.Equal(world.Id, entry.WorldId);
        Assert.Null(entry.OriginalPath);
    }

    [Fact]
    public void Find_IgnoresUnrelatedFiles()
    {
        Directory.CreateDirectory(_store.Folder);
        File.WriteAllText(Path.Combine(_store.Folder, "notes.nworld"), "not a recovery copy");

        Assert.Empty(_store.Find());
    }

    private static Dictionary<string, IAssetSource> NoAssets() => [];
}
