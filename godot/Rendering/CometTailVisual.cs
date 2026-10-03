using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Rendering;

/// <summary>
/// One comet's tail (VISION.md EVT-02; owner's choice: a glowing tail pointing away from the
/// star, longer near it): a soft cone from the comet's head, sized each frame by Core's
/// <see cref="CometTail"/> rule and compressed like every other distance in the view.
/// </summary>
public sealed class CometTailVisual
{
    private static readonly Shader _shader =
        GD.Load<Shader>("res://Rendering/comet_tail.gdshader");

    // Almost a point at the head, wider where it trails off; radii are shares of the tail's
    // length.
    private static readonly CylinderMesh _cone = new()
    {
        TopRadius = 0.003f,
        BottomRadius = 0.14f,
        Height = 1.0f,
        RadialSegments = 24,
        Rings = 1,
        CapTop = false,
        CapBottom = false,
    };

    private readonly ShaderMaterial _material = new() { Shader = _shader };

    /// <summary>
    /// Creates the tail as a child of <paramref name="parent"/>, hidden until placed.
    /// </summary>
    public CometTailVisual(Node parent)
    {
        Mesh = new MeshInstance3D
        {
            Name = "Comet tail",
            Mesh = _cone,
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        parent.AddChild(Mesh);
    }

    /// <summary>The drawn cone.</summary>
    public MeshInstance3D Mesh { get; }

    /// <summary>
    /// Places the tail with its head at <paramref name="head"/> (scene position), pointing along
    /// <paramref name="away"/> (from the star through the comet), for a comet
    /// <paramref name="distanceKm"/> from its star. Hidden when it's too far out for a tail.
    /// </summary>
    public void Place(Vector3 head, Vector3 away, double distanceKm, SystemScale scale)
    {
        double lengthKm = CometTail.LengthKm(distanceKm);
        Mesh.Visible = lengthKm > 0 && away.LengthSquared() > 0;
        if (!Mesh.Visible)
        {
            return;
        }

        var length = (float)SystemLayout.DisplayOffset(
            new Vector3D(lengthKm, 0, 0), 0, 0, scale).Length;
        Vector3 axis = away.Normalized();
        Vector3 side = axis.Cross(Mathf.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.Right)
            .Normalized();
        Vector3 across = axis.Cross(side);

        // The cone's tip (+Y) sits on the head, and its wide end trails away from the star.
        Mesh.Transform = new Transform3D(
            new Basis(side * length, -axis * length, across * length),
            head + axis * (length / 2));
        _material.SetShaderParameter("brightness", (float)CometTail.Brightness(distanceKm));
    }
}
