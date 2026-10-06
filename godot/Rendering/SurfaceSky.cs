using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;

namespace NothicWorlds.Rendering;

/// <summary>
/// The sky over a first-person eye (VISION.md REN-06), drawn by <c>surface_sky.gdshader</c>:
/// the bodies where <see cref="SkyView"/> puts them, the live weather's clouds overhead, and a
/// haze over the ground toward the horizon on worlds with air. While shown it replaces the
/// scene's environment with a copy of its own, and puts the original back when hidden, so
/// nothing about the globe's look has to be remembered and restored piece by piece.
/// </summary>
public sealed class SurfaceSky
{
    /// <summary>How many other bodies the sky draws. Must match MAX_BODIES in the shader.</summary>
    public const int MaxBodies = 12;

    /// <summary>The height of the cloud layer above the ground, in km.</summary>
    public const double CloudHeightKm = 5;

    /// <summary>Smallest a body is drawn with Magnify on, as a radius in degrees.</summary>
    public const double MagnifiedRadiusDegrees = 1.5;

    // Cloud detail is drawn some km across.
    private const double CloudDetailKm = 3;

    private static readonly Shader _shader =
        GD.Load<Shader>("res://Rendering/surface_sky.gdshader");

    private static readonly Shader _deckShader =
        GD.Load<Shader>("res://Rendering/cloud_deck.gdshader");

    private readonly ShaderMaterial _material = new() { Shader = _shader };
    private readonly Sky _sky;
    private WorldEnvironment? _scene;
    private Godot.Environment? _original;
    private Godot.Environment? _standing;

    /// <summary>Makes the sky, not shown yet.</summary>
    public SurfaceSky()
    {
        _sky = new Sky { SkyMaterial = _material, RadianceSize = Sky.RadianceSizeEnum.Size32 };
        // The clouds' fine detail, made once (on a thread of Godot's own), for both the sky
        // and the deck.
        var noise = new NoiseTexture2D
        {
            Width = 256,
            Height = 256,
            Seamless = true,
            GenerateMipmaps = true,
            Noise = new FastNoiseLite
            {
                NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
                Frequency = 1f / 32,
                FractalOctaves = 3,
            },
        };
        _material.SetShaderParameter("cloud_noise", noise);
        DeckMaterial.SetShaderParameter("cloud_noise", noise);
    }

    /// <summary>
    /// The material for the cloud deck: the clouds below an eye flying above them, drawn on
    /// rings at the cloud layer (see <see cref="ShowClouds"/>).
    /// </summary>
    public ShaderMaterial DeckMaterial { get; } = new() { Shader = _deckShader };

    /// <summary>
    /// The radius a body is drawn at in the sky, in degrees: true, or with
    /// <paramref name="magnify"/> on, at least a set size.
    /// </summary>
    public static double DrawnRadiusDegrees(SkyBody body, bool magnify) => magnify
        ? Math.Max(body.AngularDiameterDegrees / 2, MagnifiedRadiusDegrees)
        : body.AngularDiameterDegrees / 2;

    /// <summary>Puts the sky in place of <paramref name="scene"/>'s background.</summary>
    public void Show(WorldEnvironment scene)
    {
        if (_scene is not null || scene.Environment is not Godot.Environment original)
        {
            return;
        }

        _scene = scene;
        _original = original;
        _standing = (Godot.Environment)original.Duplicate();
        _standing.Sky = _sky;
        _standing.BackgroundMode = Godot.Environment.BGMode.Sky;
        // Reflections off the sky would have it filtered into a lighting map every frame (it
        // changes every frame): costly, for a shine the ground hardly shows.
        _standing.ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled;
        _standing.FogEnabled = false;
        _standing.FogMode = Godot.Environment.FogModeEnum.Exponential;
        _standing.FogAerialPerspective = 0;
        _standing.FogLightEnergy = 1;
        _standing.FogSkyAffect = 0;
        _standing.FogSunScatter = 0;
        _standing.FogHeightDensity = 0;
        _material.SetShaderParameter("space_color", original.BackgroundColor);
        scene.Environment = _standing;
    }

    /// <summary>Puts the scene's own environment back.</summary>
    public void Hide()
    {
        if (_scene is not null && _original is not null)
        {
            _scene.Environment = _original;
        }

        _scene = null;
        _original = null;
        _standing = null;
    }

    /// <summary>
    /// Draws the world's designed stars (VISION.md REN-07) on the night sky, with or without
    /// their constellation lines; none while <paramref name="stars"/> is null.
    /// </summary>
    public void ShowStars(StarSky? stars, bool lines)
    {
        if (stars is null)
        {
            _material.SetShaderParameter("has_star_field", false);
            return;
        }

        stars.ApplyTo(_material, lines);
    }

    /// <summary>
    /// Draws <paramref name="sky"/>, seen from a spot on <paramref name="ground"/> whose east,
    /// north, and up (in the scene's frame) are <paramref name="frame"/>. Small bodies are drawn
    /// bigger with <paramref name="magnify"/>; <paramref name="nebulas"/> is the night sky's
    /// backdrop, if any.
    /// </summary>
    public void ShowBodies(IReadOnlyList<Body> bodies, Body ground, SkyView sky,
        (Vector3D East, Vector3D North, Vector3D Up) frame, bool magnify, Texture2D? nebulas)
    {
        Vector3D InScene(SkyBody b) => frame.East * b.East + frame.North * b.North
            + frame.Up * b.Up;
        _material.SetShaderParameter("local_up", ToGodot(frame.Up));
        _material.SetShaderParameter("has_air", ground.HasAtmosphere);
        _material.SetShaderParameter("has_nebulas", nebulas is not null);
        if (nebulas is not null)
        {
            _material.SetShaderParameter("nebula_sky", nebulas);
        }

        _material.SetShaderParameter("has_star", sky.Star is not null);
        Vector3D starAt = Vector3D.Zero;
        if (sky.Star is SkyBody star)
        {
            Body starBody = bodies.First(b => b.Id == star.BodyId);
            starAt = InScene(star) * star.DistanceKm;
            _material.SetShaderParameter("star_direction", ToGodot(InScene(star)));
            _material.SetShaderParameter("star_radius", Radians(star, magnify));
            _material.SetShaderParameter("star_color",
                BodyAppearance.StarColor(starBody.Appearance.StarType).ToGodot());
        }

        List<SkyBody> others = [.. sky.Bodies.Where(b => b.BodyId != sky.Star?.BodyId)
            .Take(MaxBodies)];
        var directions = new Vector3[MaxBodies];
        var radii = new float[MaxBodies];
        var colors = new Vector3[MaxBodies];
        var light = new Vector3[MaxBodies];
        var shines = new float[MaxBodies];
        for (int i = 0; i < others.Count; i++)
        {
            SkyBody seen = others[i];
            Body other = bodies.First(b => b.Id == seen.BodyId);
            Vector3D direction = InScene(seen);
            Color color = other.Kind == BodyKind.Star
                ? BodyAppearance.StarColor(other.Appearance.StarType).ToGodot()
                : other.Appearance.Color.ToGodot();
            directions[i] = ToGodot(direction);
            radii[i] = Radians(seen, magnify);
            colors[i] = new Vector3(color.R, color.G, color.B);
            Vector3D toStar = starAt - direction * seen.DistanceKm;
            light[i] = toStar.Length > 0 ? ToGodot(toStar * (1 / toStar.Length)) : Vector3.Up;
            shines[i] = other.GivesLight ? 1 : 0;
        }

        _material.SetShaderParameter("body_count", others.Count);
        _material.SetShaderParameter("body_directions", directions);
        _material.SetShaderParameter("body_radii", radii);
        _material.SetShaderParameter("body_colors", colors);
        _material.SetShaderParameter("body_light", light);
        _material.SetShaderParameter("body_shines", shines);
    }

    /// <summary>
    /// Draws the clouds from the globe's live weather (<paramref name="globe"/>'s snapshots):
    /// overhead in the sky, and on the <see cref="DeckMaterial"/> for when the eye is above
    /// them. The eye is at <paramref name="eye"/> in the body's own frame and radii, over
    /// ground <paramref name="groundRadius"/> radii out; <paramref name="toBody"/> turns the
    /// scene's frame into the body's. The deck is lit for the sun's height and
    /// <paramref name="unitsPerKm"/> is the scene's scale. Returns false if there are none
    /// (no air, or no weather shown).
    /// </summary>
    public bool ShowClouds(PlanetSurface globe, Body body, Basis toBody, Vector3D eye,
        double groundRadius, double sunAltitudeDegrees, double unitsPerKm)
    {
        double eyeRadius = eye.Length;
        Vector3D up = eye * (1 / eyeRadius);
        if (!ShowCloudCover(globe, body, toBody, up, sunAltitudeDegrees, unitsPerKm))
        {
            return false;
        }

        double layer = groundRadius + CloudHeightKm / body.RadiusKm;
        _material.SetShaderParameter("flat_layer", false);
        _material.SetShaderParameter("eye_direction", ToGodot(up));
        _material.SetShaderParameter("eye_radius", (float)eyeRadius);
        // (layer − eye)(layer + eye): kept apart from the radii, too close for a float.
        _material.SetShaderParameter("cloud_gap",
            (float)((layer - eyeRadius) * (layer + eyeRadius)));
        return true;
    }

    /// <summary>
    /// Draws the clouds over a flat world's top face, as <see cref="ShowClouds"/> does over a
    /// globe: the layer is a plane over the disc, and the eye is at <paramref name="eye"/> in
    /// the body's own space, standing on <paramref name="spot"/>. None off the top face (the
    /// rim and underside are bare rock, with no weather).
    /// </summary>
    public bool ShowFlatClouds(PlanetSurface globe, Body body, Basis toBody, FlatSpot spot,
        Vector3D eye, double sunAltitudeDegrees, double unitsPerKm)
    {
        if (FlatWalk.MapDirection(spot) is not Vector3D map
            || !ShowCloudCover(globe, body, toBody, map, sunAltitudeDegrees, unitsPerKm))
        {
            _material.SetShaderParameter("has_clouds", false);
            return false;
        }

        double layer = FlatDisc.HalfThickness + CloudHeightKm / body.RadiusKm;
        _material.SetShaderParameter("flat_layer", true);
        _material.SetShaderParameter("eye_point", ToGodot(eye));
        _material.SetShaderParameter("layer_above", (float)(layer - eye.Y));
        return true;
    }

    // What the sky and the deck share: the weather, the detail's frame around the map point
    // under the eye (a globe direction), the deck's light and scale, and the body's turn.
    private bool ShowCloudCover(PlanetSurface globe, Body body, Basis toBody, Vector3D mapUp,
        double sunAltitudeDegrees, double unitsPerKm)
    {
        bool clouds = body.HasAtmosphere && globe.CopyCloudsTo(_material)
            && globe.CopyCloudsTo(DeckMaterial);
        _material.SetShaderParameter("has_clouds", clouds);
        if (!clouds)
        {
            return false;
        }

        var east = new Vector3D(mapUp.Z, 0, -mapUp.X);  // As FirstPersonGround's (any at a pole)
        east = east.Length < 1e-9 ? new Vector3D(1, 0, 0) : east * (1 / east.Length);
        var north = new Vector3D(mapUp.Y * east.Z - mapUp.Z * east.Y,
            mapUp.Z * east.X - mapUp.X * east.Z, mapUp.X * east.Y - mapUp.Y * east.X);
        foreach (ShaderMaterial material in (ShaderMaterial[])[_material, DeckMaterial])
        {
            material.SetShaderParameter("ground_east", ToGodot(east));
            material.SetShaderParameter("ground_north", ToGodot(north));
            material.SetShaderParameter("cloud_noise_scale",
                (float)(body.RadiusKm / CloudDetailKm));
        }

        DeckMaterial.SetShaderParameter("cloud_light", CloudLight(sunAltitudeDegrees));
        DeckMaterial.SetShaderParameter("units_per_km", (float)unitsPerKm);
        _material.SetShaderParameter("to_body", toBody);
        _material.SetShaderParameter("km_per_radius", (float)body.RadiusKm);
        return true;
    }

    // The light on cloud tops (as the sky lights the clouds overhead): bright by day, warmer
    // as the sun nears the horizon, dark by night.
    private static Vector3 CloudLight(double sunAltitudeDegrees)
    {
        double height = Math.Sin(double.DegreesToRadians(sunAltitudeDegrees));
        double dawn = SmoothStep(-0.1, 0.1, height);
        float light = (float)(0.006 + 0.95 * dawn * dawn);
        float low = (float)(0.5 * (1 - SmoothStep(0, 0.25, Math.Abs(height))));
        return new Vector3(1, 1, 1).Lerp(new Vector3(1, 0.55f, 0.3f), low) * light;
    }

    /// <summary>
    /// Hazes the ground toward the sky's color at the horizon with distance, on a world with
    /// air: half gone at <paramref name="halfKm"/>. <paramref name="unitsPerKm"/> is the
    /// scene's scale; the color follows the sun's height and the cloud cover (0 to 1).
    /// </summary>
    public void ShowHaze(bool air, double unitsPerKm, double halfKm, double sunAltitudeDegrees,
        double cloudCover)
    {
        if (_standing is null)
        {
            return;
        }

        _standing.FogEnabled = air;
        _standing.FogDensity = (float)(Math.Log(2) / (halfKm * unitsPerKm));

        // The sky shader's daylight, at the horizon: pale blue, greyer under cloud.
        double dawn = SmoothStep(-0.1, 0.1, Math.Sin(double.DegreesToRadians(sunAltitudeDegrees)));
        float light = (float)(0.03 + 0.97 * dawn * dawn);
        Color clear = new(0.77f, 0.86f, 0.96f), overcast = new(0.72f, 0.74f, 0.77f);
        _standing.FogLightColor = clear.Lerp(overcast, (float)Math.Clamp(cloudCover, 0, 1))
            * light;
    }

    private static double SmoothStep(double from, double to, double value)
    {
        double t = Math.Clamp((value - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static float Radians(SkyBody body, bool magnify) =>
        (float)double.DegreesToRadians(DrawnRadiusDegrees(body, magnify));

    private static Vector3 ToGodot(Vector3D v) => new((float)v.X, (float)v.Y, (float)v.Z);
}
