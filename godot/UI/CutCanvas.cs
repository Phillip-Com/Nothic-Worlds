using Godot;
using NothicWorlds.Core.Maps;

namespace NothicWorlds.UI;

/// <summary>
/// Shows an image for cutting a map piece from (VISION.md MAP-02) and lets the user draw the
/// cut. The wheel zooms around the mouse, and right- or middle-dragging pans. With the
/// rectangle tool, drag out a box. With the freeform tool, click to add points, then close the
/// shape by clicking the first point (or pressing Enter); Backspace removes the last point.
/// </summary>
public partial class CutCanvas : Control
{
    private const float FitMargin = 12.0f;
    private const float ZoomStep = 1.2f;
    private const float MaxPixelsPerImagePixel = 16.0f;
    private const float PointHandleSize = 6.0f;
    private const float CloseDistance = 10.0f;
    private const float LineWidth = 2.0f;

    private static readonly Color _lineColor = new(1.0f, 0.85f, 0.2f);
    private static readonly Color _shadeColor = new(0.0f, 0.0f, 0.0f, 0.45f);

    private readonly List<ImagePoint> _points = [];
    private Texture2D? _texture;
    private double _sourceAspectRatio = 1.0;
    private CutTool _tool = CutTool.Rectangle;
    private bool _closed;

    // The view: the image's top-left corner on screen, and screen pixels per texture pixel.
    private Vector2 _origin;
    private float _scale = 1.0f;
    // Until the user zooms or pans, the image stays fitted as the view resizes (the view has
    // no final size yet when an image is first loaded).
    private bool _keepFitted = true;

    private bool _panning;
    private ImagePoint? _rectangleStart;
    private Vector2 _mouse;

    /// <summary>Raised whenever the cut changes (drawn, closed, undone, or cleared).</summary>
    public event Action? CutChanged;

    /// <summary>The shapes a cut can be drawn with.</summary>
    public enum CutTool
    {
        Rectangle,
        Freeform,
    }

    /// <summary>The drawing tool. Changing it clears the current cut.</summary>
    public CutTool Tool
    {
        get => _tool;
        set
        {
            _tool = value;
            Clear();
        }
    }

    /// <summary>
    /// The finished cut, or null while there's none (or it's unusable, e.g. a line with no
    /// area).
    /// </summary>
    public PieceOutline? Outline { get; private set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;  // For Enter and Backspace while drawing.
        ClipContents = true;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        Resized += QueueRedraw;
    }

    /// <summary>Shows an image to cut from, fitted to the view, with no cut drawn yet.</summary>
    /// <param name="texture">What to show (may be a shrunk copy of the source image).</param>
    /// <param name="sourceAspectRatio">The original image's width divided by its height.</param>
    public void Load(Texture2D texture, double sourceAspectRatio)
    {
        _texture = texture;
        _sourceAspectRatio = sourceAspectRatio;
        _keepFitted = true;
        Clear();
    }

    /// <summary>Removes the cut, ready to draw another.</summary>
    public void Clear()
    {
        _points.Clear();
        _closed = false;
        _rectangleStart = null;
        SetOutline(null);
    }

    /// <summary>Zooms and centers so the whole image is visible.</summary>
    public void FitToView()
    {
        _keepFitted = true;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        bool handled = @event switch
        {
            InputEventMouseButton button => HandleButton(button),
            InputEventMouseMotion motion => HandleMotion(motion),
            InputEventKey { Pressed: true } key => HandleKey(key),
            _ => false,
        };

        if (handled)
        {
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        if (_texture is null)
        {
            return;
        }

        if (_keepFitted)
        {
            ApplyFit();  // Here, because only now is the view's final size known.
        }

        Vector2 imageSize = _texture.GetSize() * _scale;
        DrawTextureRect(_texture, new Rect2(_origin, imageSize), tile: false);

        List<Vector2> screenPoints = [.. _points.Select(ToScreen)];
        if (screenPoints.Count == 0)
        {
            return;
        }

        bool finished = _closed || _rectangleStart is not null;
        if (finished && screenPoints.Count >= 3)
        {
            // Shade outside the cut's bounding box so the cut stands out.
            ShadeOutside(new Rect2(screenPoints[0], Vector2.Zero), screenPoints);
        }

        if (finished)
        {
            screenPoints.Add(screenPoints[0]);
        }
        else if (_tool == CutTool.Freeform)
        {
            screenPoints.Add(_mouse);  // The next segment follows the mouse.
        }

        if (screenPoints.Count >= 2)
        {
            DrawPolyline([.. screenPoints], Colors.Black, LineWidth + 2.0f, antialiased: true);
            DrawPolyline([.. screenPoints], _lineColor, LineWidth, antialiased: true);
        }

        if (_tool == CutTool.Freeform)
        {
            foreach (ImagePoint point in _points)
            {
                var handle = new Rect2(
                    ToScreen(point) - Vector2.One * PointHandleSize / 2,
                    Vector2.One * PointHandleSize);
                DrawRect(handle, _lineColor);
                DrawRect(handle, Colors.Black, filled: false);
            }
        }
    }

    private bool HandleButton(InputEventMouseButton button)
    {
        switch (button.ButtonIndex)
        {
            case MouseButton.WheelUp when button.Pressed:
                ZoomAround(button.Position, ZoomStep);
                return true;
            case MouseButton.WheelDown when button.Pressed:
                ZoomAround(button.Position, 1.0f / ZoomStep);
                return true;
            case MouseButton.Right or MouseButton.Middle:
                _panning = button.Pressed;
                return true;
            case MouseButton.Left:
                GrabFocus();
                if (_tool == CutTool.Rectangle)
                {
                    DragRectangle(button);
                }
                else if (button.Pressed)
                {
                    AddFreeformPoint(button.Position);
                }

                return true;
            default:
                return false;
        }
    }

    private bool HandleMotion(InputEventMouseMotion motion)
    {
        _mouse = motion.Position;
        if (_panning)
        {
            _origin += motion.Relative;
            _keepFitted = false;
        }
        else if (_tool == CutTool.Rectangle && _rectangleStart is not null && !_closed)
        {
            UpdateRectangle(motion.Position);
        }

        QueueRedraw();
        return true;
    }

    private bool HandleKey(InputEventKey key)
    {
        if (_tool != CutTool.Freeform || _closed)
        {
            return false;
        }

        switch (key.Keycode)
        {
            case Key.Enter or Key.KpEnter when _points.Count >= PieceOutline.MinimumPoints:
                CloseFreeform();
                return true;
            case Key.Backspace when _points.Count > 0:
                _points.RemoveAt(_points.Count - 1);
                QueueRedraw();
                return true;
            default:
                return false;
        }
    }

    // Press starts a new rectangle, dragging sizes it, and release finishes it.
    private void DragRectangle(InputEventMouseButton button)
    {
        if (button.Pressed)
        {
            _closed = false;
            _rectangleStart = ToImage(button.Position);
            _points.Clear();
            SetOutline(null);
            return;
        }

        if (_rectangleStart is not null)
        {
            UpdateRectangle(button.Position);
            _closed = true;
        }
    }

    private void UpdateRectangle(Vector2 mouse)
    {
        ImagePoint start = _rectangleStart!.Value;
        ImagePoint end = ToImage(mouse);
        _points.Clear();
        _points.AddRange([
            start, new ImagePoint(end.U, start.V), end, new ImagePoint(start.U, end.V)]);
        SetOutline(TryCreate(() => PieceOutline.Rectangle(start, end, _sourceAspectRatio)));
    }

    private void AddFreeformPoint(Vector2 mouse)
    {
        if (_closed)
        {
            Clear();  // Clicking after finishing starts a new shape.
        }

        bool nearFirst = _points.Count >= PieceOutline.MinimumPoints
            && ToScreen(_points[0]).DistanceTo(mouse) <= CloseDistance;
        if (nearFirst)
        {
            CloseFreeform();
            return;
        }

        if (_points.Count < PieceOutline.MaximumPoints)
        {
            _points.Add(ToImage(mouse));
        }

        QueueRedraw();
    }

    private void CloseFreeform()
    {
        _closed = true;
        SetOutline(TryCreate(() => PieceOutline.Create(_points, _sourceAspectRatio)));
    }

    private void ApplyFit()
    {
        if (Size.X <= FitMargin * 2 || Size.Y <= FitMargin * 2)
        {
            return;
        }

        Vector2 imageSize = _texture!.GetSize();
        Vector2 space = Size - new Vector2(FitMargin * 2, FitMargin * 2);
        _scale = Math.Min(space.X / imageSize.X, space.Y / imageSize.Y);
        _origin = (Size - imageSize * _scale) / 2;
    }

    private void ZoomAround(Vector2 anchor, float factor)
    {
        if (_texture is null)
        {
            return;
        }

        // Never smaller than a quarter of the fitted size, never larger than 16× the texture.
        Vector2 imageSize = _texture.GetSize();
        float fitted = Math.Min(Size.X / imageSize.X, Size.Y / imageSize.Y);
        float newScale = Math.Clamp(_scale * factor, fitted / 4, MaxPixelsPerImagePixel);
        _origin = anchor - (anchor - _origin) * (newScale / _scale);
        _scale = newScale;
        _keepFitted = false;
        QueueRedraw();
    }

    // Dims the whole view except the cut's bounding box.
    private void ShadeOutside(Rect2 box, List<Vector2> shape)
    {
        foreach (Vector2 point in shape)
        {
            box = box.Expand(point);
        }

        var whole = new Rect2(Vector2.Zero, Size);
        DrawRect(new Rect2(0, 0, Size.X, box.Position.Y), _shadeColor);
        DrawRect(new Rect2(0, box.End.Y, Size.X, whole.End.Y - box.End.Y), _shadeColor);
        DrawRect(new Rect2(0, box.Position.Y, box.Position.X, box.Size.Y), _shadeColor);
        DrawRect(
            new Rect2(box.End.X, box.Position.Y, whole.End.X - box.End.X, box.Size.Y),
            _shadeColor);
    }

    private void SetOutline(PieceOutline? outline)
    {
        Outline = outline;
        QueueRedraw();
        CutChanged?.Invoke();
    }

    // Screen position → image position (0–1), kept on the image.
    private ImagePoint ToImage(Vector2 screen)
    {
        Vector2 uv = (screen - _origin) / (_texture!.GetSize() * _scale);
        return new ImagePoint(Math.Clamp(uv.X, 0, 1), Math.Clamp(uv.Y, 0, 1));
    }

    private Vector2 ToScreen(ImagePoint point)
    {
        return _origin + new Vector2((float)point.U, (float)point.V) * _texture!.GetSize() * _scale;
    }

    // A shape the user drew may be unusable (e.g. all points in a line); that's not an error,
    // it just can't be added yet.
    private static PieceOutline? TryCreate(Func<PieceOutline> create)
    {
        try
        {
            return create();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
