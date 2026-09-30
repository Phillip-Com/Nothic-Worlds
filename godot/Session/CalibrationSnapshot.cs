using NothicWorlds.Core.Maps;

namespace NothicWorlds.Session;

/// <summary>
/// What a map's calibration was when the Calibrate workspace opened, so Cancel can restore it,
/// along with whether the world already had unsaved changes.
/// </summary>
/// <param name="Calibration">The calibration before editing (null for none).</param>
/// <param name="WasUnsaved">Whether the world had unsaved changes before editing.</param>
public sealed record CalibrationSnapshot(MapCalibration? Calibration, bool WasUnsaved);
