namespace NothicWorlds.Core.Maps;

/// <summary>
/// One guide line of a <see cref="MapCalibration"/>: the true latitude (or longitude)
/// <paramref name="Degrees"/> is drawn on the map where the map type would put
/// <paramref name="DrawnAsDegrees"/>. Without calibration, the two are equal.
/// </summary>
/// <param name="Degrees">The true latitude or longitude on the globe.</param>
/// <param name="DrawnAsDegrees">
/// Where it appears on the image, expressed as the latitude or longitude the map type puts there.
/// </param>
public readonly record struct CalibrationGuide(double Degrees, double DrawnAsDegrees);
