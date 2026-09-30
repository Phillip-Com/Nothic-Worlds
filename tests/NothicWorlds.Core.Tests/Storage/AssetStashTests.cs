using NothicWorlds.Core.Storage;

namespace NothicWorlds.Core.Tests.Storage;

public sealed class AssetStashTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "nothic-worlds-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Keep_CopiesTheAsset_SoItOutlivesTheOriginal()
    {
        string original = Path.Combine(_folder, "original.png");
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(original, [1, 2, 3, 4]);
        using var stash = new AssetStash(Path.Combine(_folder, "stash"));

        FileAssetSource copy = stash.Keep("assets/abc.png", new FileAssetSource(original));
        File.Delete(original);

        using Stream stream = copy.OpenRead();
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        Assert.Equal([1, 2, 3, 4], bytes.ToArray());
    }

    [Fact]
    public void Dispose_DeletesTheStash()
    {
        string original = Path.Combine(_folder, "original.png");
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(original, [9]);
        string stashFolder = Path.Combine(_folder, "stash");
        var stash = new AssetStash(stashFolder);
        stash.Keep("assets/abc.png", new FileAssetSource(original));

        stash.Dispose();

        Assert.False(Directory.Exists(stashFolder));
    }

    [Fact]
    public void Dispose_WithNothingKept_IsFine()
    {
        new AssetStash(Path.Combine(_folder, "never-created")).Dispose();
    }
}
