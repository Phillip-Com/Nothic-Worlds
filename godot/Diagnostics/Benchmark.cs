using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Session;

namespace NothicWorlds.Diagnostics;

/// <summary>
/// Automated performance check. It orbits and zooms the camera for a fixed time with VSync
/// off, prints frame-rate and memory results, then quits. Added before a world given with
/// <c>--open=</c> opens, it first reports how long that took (VISION.md REN-03): until the
/// world showed, until every body was prepared, and the slowest frame meanwhile. Main adds it
/// when the app is started with <c>-- --benchmark</c>, e.g.
/// <c>godot --path godot --resolution 1920x1080 -- --benchmark</c>, or for a large world
/// <c>godot --path godot -- --open=world.nworld --benchmark</c>.
/// </summary>
public partial class Benchmark : Node
{
    public const string CommandLineFlag = "--benchmark";

    private const double WarmUpSeconds = 2.0;
    private const double MeasureSeconds = 10.0;
    private const float OrbitPixelsPerSecond = 150.0f;
    private const float ZoomStepsPerSecond = 8.0f;

    private double _elapsed;
    private double _loadingTime;
    private double _loadingSlowest;
    private double? _openedAfter;
    private bool _loading = true;
    private double _measuredTime;
    private int _measuredFrames;
    private double _slowestFrame;

    /// <summary>The camera to move. Must be set before the node is added to the scene.</summary>
    public PlanetCamera? Camera { get; set; }

    /// <summary>The open world, to wait for its bodies to be prepared.</summary>
    public WorldSession? Session { get; set; }

    /// <summary>
    /// Call when the world given to open has opened (or straight away with none): the
    /// benchmark waits for its bodies to be prepared, then measures.
    /// </summary>
    public void Opened() => _openedAfter = _loadingTime;

    public override void _Ready()
    {
        if (Camera is null)
        {
            GD.PushError("Benchmark needs a camera; it will not run.");
            QueueFree();
            return;
        }

        // Uncapped frame rate shows how much headroom there is above 60 fps (whatever the
        // frame-rate limit in File ▸ Settings; the other graphics options stay as set).
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = 0;
        Viewport viewport = GetViewport();
        GD.Print($"Benchmark: {RenderingServer.GetVideoAdapterName()}, " +
            $"{viewport.GetVisibleRect().Size} px, smoothing {viewport.Msaa3D}/" +
            $"{viewport.ScreenSpaceAA}, 3D at {viewport.Scaling3DScale:P0}, " +
            $"{WarmUpSeconds}s warm-up + {MeasureSeconds}s measured...");
    }

    public override void _Process(double delta)
    {
        if (_loading)
        {
            Load(delta);
            return;
        }

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

    // While the world opens and its bodies are prepared: the time it takes and the slowest
    // frame (a frame the app froze for). Reported, then measuring starts.
    private void Load(double delta)
    {
        _loadingTime += delta;
        _loadingSlowest = Math.Max(_loadingSlowest, delta);
        if (_openedAfter is not double opened || Session?.IsPreparingAnything == true)
        {
            return;
        }

        _loading = false;
        GD.Print($"Benchmark loading: opened after {opened:0.00} s, every body prepared " +
            $"after {_loadingTime:0.00} s, slowest frame meanwhile {_loadingSlowest * 1000:0} ms");
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
