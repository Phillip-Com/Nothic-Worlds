namespace NothicWorlds.Core.Model;

/// <summary>Whether a shape builds a body up or carves it away (VISION.md BOD-04).</summary>
public enum ShapeOperation
{
    /// <summary>The shape is added to the body (its walls are bare rock).</summary>
    Add,

    /// <summary>The shape is cut out of the body (the faces it leaves are bare rock).</summary>
    Cut,
}
