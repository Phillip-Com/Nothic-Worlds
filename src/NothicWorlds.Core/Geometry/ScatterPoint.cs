namespace NothicWorlds.Core.Geometry;

/// <summary>
/// A spot a plant may stand on (<see cref="PlantScatter"/>), and the even random numbers (0 to
/// 1 each) that say what it is.
/// </summary>
/// <param name="Across">How far across its tile it is.</param>
/// <param name="Down">How far down its tile it is.</param>
/// <param name="Pick">Which plant grows there, if any (<see cref="Model.PlantMix.Choose"/>).
/// </param>
/// <param name="Size">How tall it grows, within its plant's range.</param>
/// <param name="Turn">Which way it faces.</param>
/// <param name="Shape">Which of its plant's shapes it has, and how its color varies.</param>
public readonly record struct ScatterPoint(double Across, double Down, double Pick,
    double Size, double Turn, double Shape);
