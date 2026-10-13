namespace NothicWorlds.Core.Geometry;

/// <summary>
/// How far the area round a first-person eye has loaded (VISION.md REN-06; owner's choice,
/// 2026-10-12): what it's waiting on and a share done for a progress bar that never goes
/// back, until the roads, the ground round the eye, the rivers, and the near plants are in;
/// then, once ready (it stays so), whether a small note should say the rest is still loading.
/// </summary>
public sealed class AreaLoading
{
    /// <summary>
    /// How long loading must go on before the note shows, or stop before it goes, in seconds:
    /// walking, tiles come and go every moment, and the note would flicker.
    /// </summary>
    public const double NoteSeconds = 0.5;

    // Each stage's share of the bar: the ground takes most of the time.
    private const double RoadsShare = 0.1;
    private const double GroundShare = 0.7;
    private const double RiversShare = 0.1;

    private int _mostTilesLeft;
    private bool _ready;
    private double _sinceChanged;  // Seconds the loading's been going on, or stopped, as now

    /// <summary>
    /// What it's waiting on, the furthest yet: a stage once passed (the ground, say) can be
    /// due again for a moment as more comes in, but it isn't said again. Done once ready.
    /// </summary>
    public AreaLoadingStage Stage { get; private set; }

    /// <summary>How much is done, 0 to 1; it never goes back.</summary>
    public double Done { get; private set; }

    /// <summary>
    /// Whether the area round the eye is in. Once it is, it stays so, whatever loads after.
    /// </summary>
    public bool IsReady => _ready;

    /// <summary>
    /// How many pieces of the ground round the eye were still to build, last reported.
    /// </summary>
    public int TilesLeft { get; private set; }

    /// <summary>Whether the note that the rest is still loading shows (only once ready).</summary>
    public bool ShowsNote { get; private set; }

    /// <summary>
    /// Takes in how the area stands, <paramref name="seconds"/> after it was last reported.
    /// </summary>
    public void Report(AreaLoadingReport report, double seconds)
    {
        if (IsReady)
        {
            KeepNote(report.Busy, seconds);
            return;
        }

        TilesLeft = Math.Max(report.NearTilesLeft, 0);
        AreaLoadingStage due = !report.RoadsLaid ? AreaLoadingStage.Roads
            : !report.NearGroundShown ? AreaLoadingStage.Ground
            : !report.RiversShown ? AreaLoadingStage.Rivers
            : !report.PlantsPlaced ? AreaLoadingStage.Plants
            : AreaLoadingStage.Done;
        if (due == AreaLoadingStage.Done)
        {
            (_ready, Stage, Done) = (true, AreaLoadingStage.Done, 1);
            return;
        }

        // Only all of it in at once is ready; Done isn't said before then.
        Stage = (AreaLoadingStage)Math.Min(Math.Max((int)Stage, (int)due),
            (int)AreaLoadingStage.Plants);

        // The ground's share: building coarse tiles shows finer ones wanted, so the count of
        // those left can rise; the bar follows the share of the most there were that's done.
        _mostTilesLeft = Math.Max(_mostTilesLeft, TilesLeft);
        double ground = due > AreaLoadingStage.Ground ? 1
            : due < AreaLoadingStage.Ground || _mostTilesLeft == 0 ? 0
            : 1 - (double)TilesLeft / _mostTilesLeft;
        double done = (report.RoadsLaid ? RoadsShare : 0) + GroundShare * ground
            + (due > AreaLoadingStage.Rivers ? RiversShare : 0);
        Done = Math.Max(Done, Math.Min(done, 1));
    }

    // Shows the note once loading has gone on NoteSeconds, and hides it once it has stopped
    // that long.
    private void KeepNote(bool busy, double seconds)
    {
        if (busy == ShowsNote)
        {
            _sinceChanged = 0;
            return;
        }

        _sinceChanged += seconds;
        if (_sinceChanged >= NoteSeconds)
        {
            ShowsNote = busy;
            _sinceChanged = 0;
        }
    }
}
