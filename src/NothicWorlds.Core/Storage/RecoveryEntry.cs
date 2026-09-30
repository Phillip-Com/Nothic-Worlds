namespace NothicWorlds.Core.Storage;

/// <summary>A recovery copy of a world with unsaved changes (VISION.md SAV-02).</summary>
/// <param name="WorldId">The world's ID.</param>
/// <param name="RecoveryPath">The recovery copy's file, a normal <c>.nworld</c>.</param>
/// <param name="OriginalPath">Where the world was saved before, or null if it never was.</param>
/// <param name="SavedUtc">When the recovery copy was written.</param>
public sealed record RecoveryEntry(
    Guid WorldId, string RecoveryPath, string? OriginalPath, DateTimeOffset SavedUtc);
