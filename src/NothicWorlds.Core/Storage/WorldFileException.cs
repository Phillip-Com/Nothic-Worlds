namespace NothicWorlds.Core.Storage;

/// <summary>
/// A world file couldn't be read or written. The message is written for the user, e.g. "This
/// world was saved by a newer version of Nothic Worlds…".
/// </summary>
public sealed class WorldFileException : Exception
{
    public WorldFileException(string message)
        : base(message)
    {
    }

    public WorldFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
