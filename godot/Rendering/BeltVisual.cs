using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;

namespace NothicWorlds.Rendering;

/// <summary>
/// One asteroid belt's rocks (VISION.md BOD-03; owner's choice: a belt is drawn, not saved rock
/// by rock), as one batch centered on its star. Each rock's orbit is made up once from the
/// belt's own seed, so the same belt always looks the same; <c>belt_rocks.gdshader</c> moves
/// them all on the GPU as the clock runs.
/// </summary>
public sealed class BeltVisual
{
    // Rocks at full density: plenty to read as a belt, and light even on the baseline laptop.
    private const int MaxRocks = 5000;
    private const int MinRocks = 50;

    // Rocks are drawn this big for the belt's drawn width (real ones would be far too small to
    // see).
    private const double RockSizeShare = 0.012;

    private static readonly Shader _shader = GD.Load<Shader>("res://Rendering/belt_rocks.gdshader");
    private static readonly SphereMesh _rock = new()
    {
        Radius = 1.0f,
        Height = 2.0f,
        RadialSegments = 6,
        Rings = 3,
    };

    private readonly ShaderMaterial _material = new() { Shader = _shader };
    private readonly MultiMeshInstance3D _node;

    /// <summary>Creates the belt's rocks as a child of <paramref name="parent"/>.</summary>
    public BeltVisual(Node parent, AsteroidBelt belt)
    {
        Belt = belt;
        _node = new MultiMeshInstance3D
        {
            Name = $"Belt {belt.Name}",
            Multimesh = BuildRocks(belt),
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _material.SetShaderParameter("rock_color", belt.Color.ToGodot());
        parent.AddChild(_node);
    }

    /// <summary>The belt as it was when its rocks were made.</summary>
    public AsteroidBelt Belt { get; }

    /// <summary>
    /// Places the belt around its star (drawn at <paramref name="starAt"/>, with radius
    /// <paramref name="starRadius"/>) at the world's time, in the current scale.
    /// </summary>
    public void Place(Vector3 starAt, double starRadius, Body star, double timeDays,
        SystemScale scale)
    {
        _node.Position = starAt;
        double inner = SystemLayout.DisplayOffset(
            new(Belt.InnerKm, 0, 0), starRadius, 0, scale).Length;
        double outer = SystemLayout.DisplayOffset(
            new(Belt.OuterKm, 0, 0), starRadius, 0, scale).Length;
        var reach = (float)(outer * 1.1);
        _node.CustomAabb = new Aabb(-Vector3.One * reach, Vector3.One * reach * 2);
        _material.SetShaderParameter("time_days", (float)timeDays);
        _material.SetShaderParameter("year_at_au",
            (float)NewBodies.NaturalPeriodDays(CometTail.KmPerAu, star));
        _material.SetShaderParameter("true_scale", scale == SystemScale.True);
        _material.SetShaderParameter("star_radius", (float)starRadius);
        _material.SetShaderParameter("rock_size", (float)((outer - inner) * RockSizeShare));
    }

    /// <summary>Removes the rocks.</summary>
    /// <summary>Whether the belt is drawn.</summary>
    public bool Visible
    {
        get => _node.Visible;
        set => _node.Visible = value;
    }

    public void Free() => _node.QueueFree();

    // Each rock: its orbit (distance, starting angle, tilt, and tilt direction) and its look
    // (size, lumpiness, brightness), from the belt's own seed.
    private static MultiMesh BuildRocks(AsteroidBelt belt)
    {
        int count = Math.Max(MinRocks, (int)Math.Round(MaxRocks * belt.Density));
        var mesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            UseCustomData = true,
            Mesh = _rock,
            InstanceCount = count,
        };
        var random = new Random(BitConverter.ToInt32(belt.Id.ToByteArray(), 0));
        double maxTilt = double.DegreesToRadians(belt.ThicknessDegrees);
        double inner2 = belt.InnerKm * belt.InnerKm;
        double outer2 = belt.OuterKm * belt.OuterKm;
        for (int i = 0; i < count; i++)
        {
            // Spread evenly over the belt's area; most rocks tilt a little, a few a lot.
            double distance = Math.Sqrt(inner2 + (outer2 - inner2) * random.NextDouble());
            double tilt = maxTilt * (random.NextDouble() * 2 - 1) * random.NextDouble();
            mesh.SetInstanceTransform(i, Transform3D.Identity);
            mesh.SetInstanceCustomData(i, new Color((float)distance,
                (float)(random.NextDouble() * Math.Tau), (float)tilt,
                (float)(random.NextDouble() * Math.Tau)));
            double size = 0.4 + 1.2 * Math.Pow(random.NextDouble(), 3);
            mesh.SetInstanceColor(i, new Color((float)size, 0.6f + 0.8f * random.NextSingle(),
                0.6f + 0.8f * random.NextSingle(), 0.7f + 0.4f * random.NextSingle()));
        }

        return mesh;
    }
}
