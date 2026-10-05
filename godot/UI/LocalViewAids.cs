using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Measurement;
using NothicWorlds.Core.Model;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Map aids for the local view (VISION.md REN-04; owner's choice): a north arrow, a scale bar
/// with a round distance on the body's real size, and the latitude, longitude, and region under
/// the mouse. They sit at the bottom left, beside the System panel when it's open, and show
/// only in the local view.
/// </summary>
public partial class LocalViewAids : CanvasLayer
{
    private const float Margin = 12.0f;
    private const float AboveCameraText = 40.0f;
    private const float LongestBarPixels = 160.0f;
    private const float ArrowLength = 34.0f;

    private static readonly Color _ink = new(0.95f, 0.96f, 1.0f);
    private static readonly Color _shadow = new(0, 0, 0, 0.75f);

    private Control _overlay = null!;

    // Whether the aids were drawn last time, so they're cleared once on leaving the local view.
    private bool _shown;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, for the globe.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The camera, whose local view the aids describe.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>The toolbar: the aids hide whenever it does.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>The System panel: the aids move aside while it's open.</summary>
    [Export] public SystemPanel? SystemPanel { get; set; }

    public override void _Ready()
    {
        _overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.Draw += DrawAids;
        AddChild(_overlay);
        if (Session is null || System is null || Camera is null)
        {
            GD.PushError("LocalViewAids needs a world session, system view, and camera.");
            return;
        }

        // Only redrawn in the local view, where the ground moves under the aids.
        System.Placed += () =>
        {
            if (Camera.IsLocalView || _shown)
            {
                _overlay.QueueRedraw();
            }
        };
        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += () => Visible = Toolbar.Visible;
        }
    }

    private void DrawAids()
    {
        _shown = false;
        if (!Visible || Camera is not { IsLocalView: true }
            || System?.SurfaceFor(Session!.SelectedBodyId) is not PlanetSurface globe)
        {
            return;
        }

        Vector2 size = _overlay.GetRect().Size;
        Vector2 middle = size / 2;
        if (GlobePicker.CoordinateAt(Camera, globe, middle) is not GeoCoordinate centre)
        {
            return;
        }

        _shown = true;
        Body body = Session.SelectedBody;
        float left = Margin
            + (SystemPanel is { IsPanelOpen: true } ? SystemPanel.PanelWidth + Margin : 0);
        float bottom = size.Y - AboveCameraText;
        Font font = _overlay.GetThemeDefaultFont();

        DrawNorthArrow(globe, centre, new Vector2(left + ArrowLength / 2, bottom - 60), font);
        DrawScaleBar(globe, body, middle, new Vector2(left + ArrowLength + Margin, bottom - 40),
            font);
        DrawMouseSpot(globe, body, new Vector2(left, bottom), font);
    }

    // An arrow pointing to the body's north from the middle of the view.
    private void DrawNorthArrow(PlanetSurface globe, GeoCoordinate centre, Vector2 at, Font font)
    {
        var ahead = new GeoCoordinate(Math.Min(centre.LatitudeDegrees + 0.01, 90),
            centre.LongitudeDegrees);
        if (GlobePicker.ScreenPositionOf(Camera!, globe, centre) is not Vector2 from
            || GlobePicker.ScreenPositionOf(Camera!, globe, ahead) is not Vector2 to
            || from.DistanceTo(to) < 1e-4f)
        {
            return;
        }

        Vector2 north = (to - from).Normalized();
        Vector2 tip = at + north * ArrowLength / 2;
        Vector2 tail = at - north * ArrowLength / 2;
        Vector2 side = new Vector2(-north.Y, north.X) * 7;
        Vector2[] head = [tip, tip - north * 12 + side, tip - north * 12 - side];
        _overlay.DrawLine(tail, tip, _shadow, 5);
        _overlay.DrawColoredPolygon(head, _shadow);
        _overlay.DrawLine(tail, tip, _ink, 2);
        _overlay.DrawColoredPolygon(
            [tip, tip - north * 10 + side * 0.7f, tip - north * 10 - side * 0.7f], _ink);
        Text(font, tip + north * 6 + new Vector2(-4, north.Y < 0 ? 0 : 12), "N");
    }

    // A bar showing a round distance (1, 2, or 5 × a power of ten km) across the ground.
    private void DrawScaleBar(
        PlanetSurface globe, Body body, Vector2 middle, Vector2 at, Font font)
    {
        // Measured across the middle of the view, on the real body's size (across a flat
        // world's face, which is its real ground).
        const float sample = 100.0f;
        if (GlobePicker.CoordinateAt(Camera!, globe, middle) is not GeoCoordinate a
            || GlobePicker.CoordinateAt(Camera!, globe, middle + new Vector2(sample, 0))
                is not GeoCoordinate b)
        {
            return;
        }

        double kmPerPixel = SurfaceDistance.Km(body.Shape, body.RadiusKm, a, b) / sample;
        if (kmPerPixel <= 0)
        {
            return;
        }

        // A round number of km or miles (by the setting).
        double perPixel = UnitText.Shown(Quantity.Distance, kmPerPixel);
        double shown = RoundDistance(perPixel * LongestBarPixels);
        float length = (float)(shown / perPixel);
        Vector2 end = at + new Vector2(length, 0);
        foreach ((Color color, float width) in new[] { (_shadow, 5f), (_ink, 2f) })
        {
            _overlay.DrawLine(at, end, color, width);
            _overlay.DrawLine(at + new Vector2(0, -6), at + new Vector2(0, 6), color, width);
            _overlay.DrawLine(end + new Vector2(0, -6), end + new Vector2(0, 6), color, width);
        }

        Text(font, at + new Vector2(0, -10),
            $"{shown:#,0.###} {UnitText.Symbol(Quantity.Distance)}");
    }

    // The latitude, longitude, and regions at the mouse.
    private void DrawMouseSpot(PlanetSurface globe, Body body, Vector2 at, Font font)
    {
        if (GlobePicker.CoordinateAt(Camera!, globe, _overlay.GetLocalMousePosition())
            is not GeoCoordinate spot)
        {
            return;
        }

        string regions = string.Join(", ",
            LoreRules.RegionsAt(Session!.World, body.Id, spot).Select(r => r.Name));
        Text(font, at, regions.Length == 0
            ? PlaceText.Describe(spot)
            : $"{PlaceText.Describe(spot)} · {regions}");
    }

    private void Text(Font font, Vector2 at, string text)
    {
        _overlay.DrawString(font, at + Vector2.One, text, modulate: Colors.Black);
        _overlay.DrawString(font, at, text, modulate: _ink);
    }

    // The largest 1, 2, or 5 × a power of ten not more than `most`.
    private static double RoundDistance(double most)
    {
        double power = Math.Pow(10, Math.Floor(Math.Log10(most)));
        return most >= 5 * power ? 5 * power : most >= 2 * power ? 2 * power : power;
    }
}
