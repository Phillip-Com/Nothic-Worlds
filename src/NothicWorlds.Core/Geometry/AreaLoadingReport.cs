namespace NothicWorlds.Core.Geometry;

/// <summary>
/// How the area round a first-person eye stands this frame (<see cref="AreaLoading"/>).
/// </summary>
/// <param name="RoadsLaid">The roads have been laid (or there are none).</param>
/// <param name="NearGroundShown">The ground round the eye is built and drawn (or isn't drawn
/// here at all).</param>
/// <param name="NearTilesLeft">How many pieces of that ground are still to build.</param>
/// <param name="RiversShown">The rivers and the roads' paving near the eye are drawn.</param>
/// <param name="PlantsPlaced">The plants near the eye are placed on the ground drawn (or there
/// are none to place).</param>
/// <param name="Busy">Anything at all is still loading, the far ground and trees too.</param>
public readonly record struct AreaLoadingReport(bool RoadsLaid, bool NearGroundShown,
    int NearTilesLeft, bool RiversShown, bool PlantsPlaced, bool Busy);
