using Godot;
using NothicWorlds.Controls;

namespace NothicWorlds.Diagnostics;

/// <summary>
/// On-screen performance numbers (frame rate, memory, draw calls), toggled with F3.
/// Hidden by default. Refreshes a few times per second to keep its own cost negligible.
/// </summary>
public partial class PerformanceOverlay : CanvasLayer
{
    private const double RefreshSeconds = 0.25;
    private const int ScreenMargin = 12;
    private const double BytesPerMegabyte = 1024.0 * 1024.0;

    private Label _label = null!;
    private double _secondsSinceRefresh = RefreshSeconds;

    public override void _Ready()
    {
        // Top-right, so it doesn't cover the map toolbar in the top-left.
        _label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            GrowHorizontal = Control.GrowDirection.Begin,
        };
        _label.SetAnchorsAndOffsetsPreset(
            Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, ScreenMargin);
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        AddChild(_label);
        Visible = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.TogglePerformanceOverlay))
        {
            Visible = !Visible;
            _secondsSinceRefresh = RefreshSeconds;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _secondsSinceRefresh += delta;
        if (_secondsSinceRefresh < RefreshSeconds)
        {
            return;
        }

        _secondsSinceRefresh = 0;
        double fps = Engine.GetFramesPerSecond();
        _label.Text =
            $"FPS: {fps:0} ({(fps > 0 ? 1000.0 / fps : 0):0.0} ms)\n" +
            $"Video memory: {Megabytes(Performance.Monitor.RenderVideoMemUsed):0} MB\n" +
            $"App memory: {Megabytes(Performance.Monitor.MemoryStatic):0} MB\n" +
            $"Draw calls: {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0}";
    }

    private static double Megabytes(Performance.Monitor monitor)
    {
        return Performance.GetMonitor(monitor) / BytesPerMegabyte;
    }
}
