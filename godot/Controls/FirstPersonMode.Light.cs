using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;
using NothicWorlds.Rendering;

namespace NothicWorlds.Controls;

/// <summary>
/// The light while standing (VISION.md REN-06; owner's choices, 2026-10-10): the sun's own
/// light from where it stands in the sky, reddened and dimmed by the air as it sets, casting
/// shadows near the eye as far as File ▸ Settings, Standing shadows says
/// (<see cref="StandingSun"/>).
/// </summary>
public partial class FirstPersonMode
{
    private StandingSun? _sun;
    private StandingShadowDetail _standingShadows = StandingShadowDetail.Standard;

    /// <summary>
    /// How far the sun's shadows reach around the eye, and how sharp (File ▸ Settings,
    /// Standing shadows): changed while standing, it shows at once.
    /// </summary>
    public StandingShadowDetail StandingShadows
    {
        get => _standingShadows;
        set
        {
            _standingShadows = value;
            if (_sun is not null)
            {
                _sun.Detail = value;
            }
        }
    }

    // Puts the sun in place of the star's light for the standing camera; the minimap keeps
    // the star's.
    private void MakeSun()
    {
        _sun = new StandingSun { Detail = _standingShadows };
        AddChild(_sun);
        _camera!.CullMask &= ~SystemView.StarLightLayer;
        _overheadCamera!.CullMask &= ~StandingSun.Layer;
    }

    private void FreeSun()
    {
        _sun?.QueueFree();
        _sun = null;
    }

    // Lights the view from the body's star as the sky shows it, from an eye `eyeKm` above the
    // base; dark with no star, or once it has set.
    private void ShowSunlight(Body body, SkyView sky, (Vector3D East, Vector3D North,
        Vector3D Up) frame, double eyeKm, double unitsPerMeter)
    {
        if (_sun is null)
        {
            return;
        }

        if (sky.Star is not SkyBody star
            || Session?.World.Bodies.Find(b => b.Id == star.BodyId) is not Body starBody)
        {
            _sun.Show(Vector3.Zero, Colors.Black, 0, unitsPerMeter);
            return;
        }

        var light = SunlightThroughAir.At(star.AltitudeDegrees, eyeKm, body.HasAtmosphere);
        // The star's own light is its color lightened, as SystemView lights the globe.
        Color color = BodyAppearance.StarColor(starBody.Appearance.StarType).ToGodot()
            .Lightened(0.6f) * new Color((float)light.Red, (float)light.Green, (float)light.Blue);
        Vector3D toward = frame.East * star.East + frame.North * star.North
            + frame.Up * star.Up;
        _sun.Show(ToGodot(toward), color, (float)light.Strength, unitsPerMeter);
    }
}
