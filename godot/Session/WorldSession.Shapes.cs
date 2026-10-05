using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Session;

// The shapes part of the open world (VISION.md BOD-04): shapes added to or cut out of planets
// and moons, all undoable.
public partial class WorldSession
{
    /// <summary>
    /// Places a new shape on the selected body at <paramref name="spot"/>, sized to suit the
    /// body: an added one sits on the ground, a cut one is half sunk into it (a pit, or a
    /// crater for a sphere). One undo step.
    /// </summary>
    /// <param name="spot">Where on the body.</param>
    /// <param name="kind">Which kind of shape.</param>
    /// <param name="operation">Whether it adds or cuts.</param>
    /// <param name="groundKm">
    /// The height of the ground it goes on, in km above the radius, when that isn't the sculpted
    /// ground at the spot (such as the floor of a hole another shape cut); null for that ground.
    /// </param>
    /// <returns>The new shape, or null if it couldn't be placed (and why).</returns>
    public (ShapeEdit? Shape, string? Problem) AddShape(
        GeoCoordinate spot, ShapeKind kind, ShapeOperation operation, double? groundKm = null)
    {
        if (!SelectedBodyCanBeSculpted)
        {
            return (null, "shapes go on planets and moons shaped as globes");
        }

        Body body = SelectedBody;
        if (body.Surface.Shapes.Count >= ShapeEdit.MaxPerBody)
        {
            return (null, $"a body can have up to {ShapeEdit.MaxPerBody} shapes");
        }

        double width = Math.Max(1, Math.Round(body.RadiusKm * 0.05));
        double height = kind == ShapeKind.Sphere ? width : Math.Max(1, Math.Round(width * 0.3));
        double ground = groundKm ?? HeightAt(spot) / 1000;
        double depth = operation == ShapeOperation.Add && kind != ShapeKind.Sphere
            ? ground + height / 2
            : ground;
        var shape = new ShapeEdit(Guid.NewGuid(), kind, operation, spot, depth, width, height,
            width, 0);
        RecordUndo($"Add {kind}");
        body.Surface.Shapes.Add(shape);
        ShowTerrain(body);
        MarkChanged(systemChanged: false);
        return (shape, null);
    }

    /// <summary>
    /// Replaces one of the selected body's shapes (found by its ID) with
    /// <paramref name="changed"/>. Rapid changes to the same shape (typing in its fields) are
    /// one undo step; a drag on the globe is one step too.
    /// </summary>
    /// <returns>What's wrong with the change (nothing is changed then), or null.</returns>
    public string? UpdateShape(ShapeEdit changed, bool mergeWithLast = true)
    {
        Body body = SelectedBody;
        int index = body.Surface.Shapes.FindIndex(shape => shape.Id == changed.Id);
        if (index < 0 || body.Surface.Shapes[index] == changed)
        {
            return null;
        }

        if (changed.Problem(body.RadiusKm) is string problem)
        {
            return problem;
        }

        RecordUndo($"Edit {changed.Operation} {changed.Kind}",
            mergeKey: mergeWithLast ? ("shape", changed.Id) : null);
        body.Surface.Shapes[index] = changed;
        ShowTerrain(body);
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>Removes one of the selected body's shapes. One undo step.</summary>
    public void DeleteShape(Guid shapeId)
    {
        Body body = SelectedBody;
        int index = body.Surface.Shapes.FindIndex(shape => shape.Id == shapeId);
        if (index < 0)
        {
            return;
        }

        ShapeEdit shape = body.Surface.Shapes[index];
        RecordUndo($"Delete {shape.Kind}");
        body.Surface.Shapes.RemoveAt(index);
        ShowTerrain(body);
        MarkChanged(systemChanged: false);
    }
}
