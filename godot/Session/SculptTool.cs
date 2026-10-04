namespace NothicWorlds.Session;

/// <summary>The sculpting brushes (VISION.md BOD-04; owner's choice: these four).</summary>
public enum SculptTool
{
    /// <summary>Pushes the ground up by a height.</summary>
    Raise,

    /// <summary>Pushes the ground down by a height.</summary>
    Lower,

    /// <summary>Evens out bumps, by an amount.</summary>
    Smooth,

    /// <summary>Levels the ground to where the stroke began, by an amount.</summary>
    Flatten,
}
