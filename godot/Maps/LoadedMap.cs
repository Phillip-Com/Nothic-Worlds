using Godot;
using NothicWorlds.Core.Maps;

namespace NothicWorlds.Maps;

/// <summary>
/// A map image that's ready to apply to a planet, plus what was found when checking it.
/// </summary>
/// <param name="Texture">The map, sized and with mipmaps, ready for the GPU.</param>
/// <param name="Check">The size and layout check, e.g. whether it was shrunk or isn't 2:1.</param>
public sealed record LoadedMap(Texture2D Texture, MapImageCheck Check);
