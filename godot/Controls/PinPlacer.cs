using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Rendering;
using NothicWorlds.Session;
using NothicWorlds.UI;

namespace NothicWorlds.Controls;

/// <summary>
/// Places a pin by clicking on a globe (VISION.md LORE-02; owner's choice): flies to the body,
/// asks for a click on the spot, and hands back the latitude/longitude clicked. Esc cancels.
/// Clicks that miss the globe pass through, so the camera can still be turned meanwhile.
/// </summary>
/// <remarks>
/// Must come after the camera and the markers in the scene: later nodes get unhandled input
/// first, and this one takes the click that places the pin.
/// </remarks>
public partial class PinPlacer : Node
{
    private (Guid BodyId, Action<GeoCoordinate> Placed, Action? Cancelled)? _placing;

    /// <summary>The open world.</summary>
    // Whether what's being placed is a pin (its messages are shown) or something else.
    private bool _isPin = true;

    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, for the body's globe.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The camera, for turning the click into a spot on the globe.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>Where the "click the spot" hint goes.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>True while waiting for the click.</summary>
    public bool IsPlacing => _placing is not null;

    /// <summary>
    /// Starts placing a pin on a body (a planet or moon): flies there, then waits for a click on
    /// its surface. <paramref name="placed"/> gets the spot; <paramref name="cancelled"/> is
    /// called instead if Esc is pressed or placing becomes impossible. With a
    /// <paramref name="prompt"/>, it's for something other than a pin (e.g. where to stand):
    /// that's shown instead, and the pin's own messages aren't.
    /// </summary>
    public async void Start(Guid bodyId, Action<GeoCoordinate> placed, Action? cancelled = null,
        string? prompt = null)
    {
        Cancel();
        if (Session?.World.Bodies.FirstOrDefault(b => b.Id == bodyId) is not Body body
            || !body.HasSurface)
        {
            cancelled?.Invoke();
            return;
        }

        _placing = (bodyId, placed, cancelled);
        _isPin = prompt is null;
        Toolbar?.ShowInfo(prompt ?? $"Click the spot on {body.Name} for the pin (Esc cancels).",
            autoHide: false);
        if (Session.SelectedBodyId != bodyId
            && await Session.SelectBodyAsync(bodyId) is string warning)
        {
            Toolbar?.ShowWarning(warning);
        }
    }

    /// <summary>Stops waiting for a click, if placing.</summary>
    public void Cancel()
    {
        if (_placing is { } placing)
        {
            _placing = null;
            Toolbar?.ShowInfo(_isPin ? "Pin not placed." : "");
            placing.Cancelled?.Invoke();
        }
    }

    public override void _Ready()
    {
        Session!.WorldClosed += _ => Cancel();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_placing is not (Guid bodyId, Action<GeoCoordinate> placed, _))
        {
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Cancel();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click
            && System?.SurfaceFor(bodyId) is PlanetSurface globe && Camera is not null
            && GlobePicker.CoordinateAt(Camera, globe, click.Position) is GeoCoordinate spot)
        {
            _placing = null;
            Toolbar?.ShowInfo(_isPin ? $"Pinned at {PlaceText.Describe(spot)}." : "");
            placed(spot);
            GetViewport().SetInputAsHandled();
        }
    }
}
