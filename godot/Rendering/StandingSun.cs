using Godot;

namespace NothicWorlds.Rendering;

/// <summary>
/// The sun's light while standing (VISION.md REN-06): parallel rays from where the sky shows
/// the sun, colored and dimmed by the air it comes through, casting shadows near the eye as
/// far as <see cref="Detail"/> says. It takes the place of the star's own light (a point light
/// far off, which can't cast shadows at this scale) for the standing camera, which leaves out
/// <see cref="SystemView.StarLightLayer"/>; the minimap's camera leaves out this one's
/// <see cref="Layer"/> and keeps the star's, so it isn't shadowed twice.
/// </summary>
public sealed partial class StandingSun : DirectionalLight3D
{
    /// <summary>The render layer the sun is on (layer 19), which the minimap leaves out.</summary>
    public const uint Layer = 1u << 18;

    /// <summary>Makes the sun, with no shadows until shown.</summary>
    public StandingSun()
    {
        Name = "StandingSun";
        Layers = Layer;
        ShadowEnabled = false;
        DirectionalShadowBlendSplits = true;
    }

    /// <summary>How far the shadows reach, and how sharp; none at Off.</summary>
    public StandingShadowDetail Detail { get; set; } = StandingShadowDetail.Standard;

    /// <summary>How far from the eye the shadows reach, in meters (0 at Off).</summary>
    public double ReachMeters => Detail switch
    {
        StandingShadowDetail.Off => 0,
        StandingShadowDetail.Low => 50,
        StandingShadowDetail.High => 300,
        _ => 100,
    };

    /// <summary>
    /// Points the light from <paramref name="towardSun"/> (in the scene, from the eye at its
    /// middle), colored <paramref name="color"/> and as strong as <paramref name="strength"/>
    /// (0 with the sun set: then it's off). <paramref name="unitsPerMeter"/> is the scene's
    /// scale, which sets the shadows' reach.
    /// </summary>
    public void Show(Vector3 towardSun, Color color, float strength, double unitsPerMeter)
    {
        Visible = strength > 0.001f && towardSun != Vector3.Zero;
        if (!Visible)
        {
            return;
        }

        Vector3 along = -towardSun.Normalized();
        Vector3 up = Math.Abs(along.Y) > 0.99f ? Vector3.Right : Vector3.Up;
        Transform = new Transform3D(Basis.LookingAt(along, up), Vector3.Zero);
        LightColor = color;
        LightEnergy = strength;

        double meters = ReachMeters;
        // Low's smaller map is lighter to draw (owner's choice, 2026-10-11), its shorter reach
        // keeping it about as sharp.
        (ShadowMode mode, int size) = Detail switch
        {
            StandingShadowDetail.Low => (ShadowMode.Orthogonal, 1024),
            StandingShadowDetail.High => (ShadowMode.Parallel2Splits, 4096),
            _ => (ShadowMode.Orthogonal, 2048),
        };
        ShadowEnabled = Detail != StandingShadowDetail.Off;
        DirectionalShadowMode = mode;
        DirectionalShadowMaxDistance = (float)(meters * unitsPerMeter);
        DirectionalShadowSplit1 = 0.25f;
        // Casters behind the shadows' patch are flattened onto it from only as far back again
        // (the default is 20 units, most of a world here), or the depth's precision would be
        // spent on empty air.
        DirectionalShadowPancakeSize = (float)(meters * unitsPerMeter);
        if (ShadowEnabled && size != _atlasSize)
        {
            RenderingServer.DirectionalShadowAtlasSetSize(size, true);
            _atlasSize = size;
        }
    }

    private static int _atlasSize;
}
