using NothicWorlds.Core.Maps;

namespace NothicWorlds.Session;

/// <summary>
/// What a map's calibration was when the Calibrate workspace opened, so Cancel can restore it.
/// </summary>
/// <param name="Calibration">The calibration before editing (null for none).</param>
public sealed record CalibrationSnapshot(MapCalibration? Calibration);
