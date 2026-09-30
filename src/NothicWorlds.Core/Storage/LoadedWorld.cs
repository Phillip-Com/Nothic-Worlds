using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Storage;

/// <summary>A world read from a file, plus where to read each of its assets.</summary>
/// <param name="World">The world's data.</param>
/// <param name="Assets">
/// Every asset the world references, by name, each pointing into the world file.
/// </param>
public sealed record LoadedWorld(World World, IReadOnlyDictionary<string, IAssetSource> Assets);
