using NothicWorlds.Core.Storage;

namespace NothicWorlds.Core.Tests.Storage;

public class RecentWorldsTests
{
    [Fact]
    public void Add_PutsTheWorldFirst()
    {
        IReadOnlyList<string> recent =
            RecentWorlds.Add(["C:/a.nworld", "C:/b.nworld"], "C:/c.nworld");

        Assert.Equal(["C:/c.nworld", "C:/a.nworld", "C:/b.nworld"], recent);
    }

    [Fact]
    public void Add_MovesAWorldAlreadyListed_TreatingCaseAndSlashesAsTheSame()
    {
        IReadOnlyList<string> recent =
            RecentWorlds.Add(["C:/a.nworld", "C:/Worlds/B.nworld"], @"c:\worlds\b.nworld");

        Assert.Equal([@"c:\worlds\b.nworld", "C:/a.nworld"], recent);
    }

    [Fact]
    public void Add_KeepsOnlyTheNewest()
    {
        IEnumerable<string> full =
            Enumerable.Range(0, RecentWorlds.MaxCount).Select(i => $"{i}.nworld");

        IReadOnlyList<string> recent = RecentWorlds.Add(full, "new.nworld");

        Assert.Equal(RecentWorlds.MaxCount, recent.Count);
        Assert.Equal("new.nworld", recent[0]);
        Assert.DoesNotContain($"{RecentWorlds.MaxCount - 1}.nworld", recent);
    }

    [Fact]
    public void Remove_TakesTheWorldOut()
    {
        Assert.Equal(["C:/a.nworld"],
            RecentWorlds.Remove(["C:/a.nworld", "C:/b.nworld"], "C:/B.nworld"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Add_RejectsAnEmptyPath(string path)
    {
        Assert.Throws<ArgumentException>(() => RecentWorlds.Add([], path));
    }
}
