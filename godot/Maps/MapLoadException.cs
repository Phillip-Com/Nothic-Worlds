namespace NothicWorlds.Maps;

/// <summary>
/// A map image couldn't be imported. The message is written for the user and completes the
/// sentence "Couldn't import &lt;file&gt;: ...".
/// </summary>
public sealed class MapLoadException : Exception
{
    public MapLoadException(string message)
        : base(message)
    {
    }
}
