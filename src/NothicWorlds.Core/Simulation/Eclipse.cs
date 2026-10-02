namespace NothicWorlds.Core.Simulation;

/// <summary>One eclipse (see <see cref="Eclipses"/>). Times are in standard days.</summary>
/// <param name="Kind">Solar or lunar, from the selected body's point of view.</param>
/// <param name="Type">How deep it goes.</param>
/// <param name="BlockerId">The body casting the shadow.</param>
/// <param name="ShadowedId">The body the shadow falls on.</param>
/// <param name="StartDays">When the outer shadow first touches the shadowed body.</param>
/// <param name="PeakDays">When the shadowed body is closest to the shadow's center.</param>
/// <param name="EndDays">When the outer shadow last touches it.</param>
/// <param name="Coverage">
/// How much is covered at the peak, from 0 to 1. Solar: the share of the star's disk hidden,
/// seen from the best spot on the body (1 for a total eclipse). Lunar: the share of the moon's
/// width in full shadow (0 for a penumbral eclipse, 1 for a total one).
/// </param>
public readonly record struct Eclipse(
    EclipseKind Kind,
    EclipseType Type,
    Guid BlockerId,
    Guid ShadowedId,
    double StartDays,
    double PeakDays,
    double EndDays,
    double Coverage);
