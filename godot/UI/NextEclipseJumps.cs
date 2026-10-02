using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Quick jumps in the Go to dialog (VISION.md EVT-01; owner's request): the selected body's
/// next solar and next lunar eclipse, each with its date and a button that jumps to its peak.
/// </summary>
public partial class NextEclipseJumps : VBoxContainer
{
    private readonly Label[] _texts = new Label[2];
    private readonly Button[] _buttons = new Button[2];
    private readonly double[] _peaks = new double[2];

    /// <summary>The open world. Set it before adding the rows to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>
    /// Called with the peak's time (standard days) when a jump button is pressed.
    /// </summary>
    public Action<double>? Jump { get; init; }

    public override void _Ready()
    {
        AddChild(new HSeparator());
        foreach (EclipseKind kind in new[] { EclipseKind.Solar, EclipseKind.Lunar })
        {
            int index = (int)kind;
            var row = new HBoxContainer();
            _texts[index] = new Label
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(260, 0),
            };
            _buttons[index] = new Button
            {
                Text = "Go",
                FocusMode = FocusModeEnum.None,
                TooltipText = "Run the clock to this eclipse's peak",
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            _buttons[index].Pressed += () => Jump?.Invoke(_peaks[index]);
            row.AddChild(_texts[index]);
            row.AddChild(_buttons[index]);
            AddChild(row);
        }

        // The eclipses may still be being worked out when the dialog opens.
        Session.EclipsesReady += Refresh;
    }

    public override void _ExitTree()
    {
        Session.EclipsesReady -= Refresh;
    }

    /// <summary>Shows the selected body's next solar and lunar eclipse.</summary>
    public void Refresh()
    {
        if (!IsNodeReady())
        {
            return;
        }

        Body body = Session.SelectedBody;
        EclipseTimeline? timeline = Session.SelectedEclipses;
        double now = Session.TimeDays;
        foreach (EclipseKind kind in new[] { EclipseKind.Solar, EclipseKind.Lunar })
        {
            int index = (int)kind;
            string name = kind == EclipseKind.Solar ? "solar" : "lunar";
            Eclipse? next = timeline?.Eclipses
                .Where(e => e.Kind == kind && e.PeakDays > now)
                .Cast<Eclipse?>()
                .FirstOrDefault();
            _buttons[index].Visible = next is not null;
            if (next is Eclipse eclipse)
            {
                _peaks[index] = eclipse.PeakDays;
                string type = eclipse.Type.ToString().ToLowerInvariant();
                _texts[index].Text = $"Next {name} eclipse ({type}): " +
                    BodyClock.Describe(body, eclipse.PeakDays);
            }
            else
            {
                _texts[index].Text = timeline is null
                    ? $"Next {name} eclipse: working it out…"
                    : $"Next {name} eclipse: none in the coming year";
            }
        }
    }
}
