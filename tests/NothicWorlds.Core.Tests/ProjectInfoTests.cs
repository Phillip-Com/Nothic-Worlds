namespace NothicWorlds.Core.Tests;

/// <summary>
/// Placeholder test that proves the test project builds and can reach the core library.
/// Remove once real tests exist.
/// </summary>
public class ProjectInfoTests
{
    [Fact]
    public void Name_IsNothicWorlds()
    {
        Assert.Equal("Nothic Worlds", ProjectInfo.Name);
    }
}
