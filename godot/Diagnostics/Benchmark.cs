using Godot;
using NothicWorlds.Controls;

namespace NothicWorlds.Diagnostics;

/// <summary>
/// Automated performance check. It orbits and zooms the camera for a fixed time with VSync
/// off, prints frame-rate and memory results, then quits. Main adds it when the app is started
/// with <c>-- --benchmark</c>, e.g.
/// <c>godot --path godot --resolution 1920x1080 -- --benchmark</c>.
/// </summary>
public partial class Benchmark : Node
{
    public const string CommandLineFlag = "--benchmark";

    private const double WarmUpSeconds = 2.0;
    private const double MeasureSeconds = 10.0;
    private const float OrbitPixelsPerSecond = 150.0f;
    private const float ZoomStepsPerSecond = 8.0f;

    private double _elapsed;
    private double _measuredTime;
    private int _measuredFrames;
    private double _slowestFrame;

    /// <summary>The camera to move. Must be set before the node is added to the scene.</summary>
    public PlanetCamera? Camera { get; set; }

    public override void _Ready()
    {
        if (Camera is null)
        {
            GD.PushError("Benchmark needs a camera; it will not run.");
            QueueFree();
            return;
        }

        // Uncapped frame rate shows how much headroom there is above 60 fps.
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        GD.Print($"Benchmark: {RenderingServer.GetVideoAdapterName()}, " +
            $"{GetViewport().GetVisibleRect().Size} px, " +
            $"{WarmUpSeconds}s warm-up + {MeasureSeconds}s measured...");
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;

        // Orbit steadily while zooming in and out, to cover near and far views.
        Camera!.Orbit(new Vector2(OrbitPixelsPerSecond * (float)delta, 0));
        Camera.Zoom(Mathf.Sin((float)_elapsed) * ZoomStepsPerSecond * (float)delta);

        if (_elapsed < WarmUpSeconds)
        {
            return;
        }

        _measuredTime += delta;
        _measuredFrames++;
        _slowestFrame = Math.Max(_slowestFrame, delta);

        if (_measuredTime >= MeasureSeconds)
        {
            Report();
            GetTree().Quit();
        }
    }

    private void Report()
    {
        const double bytesPerMegabyte = 1024.0 * 1024.0;
        double videoMemory =
            Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / bytesPerMegabyte;
        double appMemory =
            Performance.GetMonitor(Performance.Monitor.MemoryStatic) / bytesPerMegabyte;

        GD.Print($"Benchmark result: average {_measuredFrames / _measuredTime:0} fps, " +
            $"slowest frame {_slowestFrame * 1000.0:0.0} ms " +
            $"({1.0 / _slowestFrame:0} fps), " +
            $"video memory {videoMemory:0} MB, app memory {appMemory:0} MB");
    }
}
