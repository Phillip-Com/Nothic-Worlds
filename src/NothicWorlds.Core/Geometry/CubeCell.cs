namespace NothicWorlds.Core.Geometry;

/// <summary>
/// One cell of a <see cref="CubeSphere"/> grid: which face (0 to 5) and where on it (row 0 is
/// the top of the face).
/// </summary>
public readonly record struct CubeCell(int Face, int Column, int Row);
