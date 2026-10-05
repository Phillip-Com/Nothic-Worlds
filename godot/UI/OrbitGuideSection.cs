using System.Globalization;
using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The System panel's stable orbit guide (VISION.md SIM-04), in numbers: where the selected
/// body's moons would stay steady, its Roche limit and Hill sphere, any warnings about its
/// orbit, and the period gravity would give its orbit, with a button to use it. Orbits stay
/// as designed unless that button is pressed.
/// </summary>
public partial class OrbitGuideSection : VBoxContainer
{
    private const double KmPerAu = 149_597_870.7;

    private static readonly Color _warningColor = new(1.0f, 0.8f, 0.35f);

    private Label _moons = null!;
    private Label _limits = null!;
    private Label _warnings = null!;
    private HBoxContainer _periodRow = null!;
    private Label _period = null!;
    private Button _usePeriod = null!;
    private double _naturalPeriod;

    /// <summary>The open world. Set it before adding the section to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>Shows why an edit was refused (the panel's message line).</summary>
    public Action<string?>? ReportProblem { get; init; }

    public override void _Ready()
    {
        AddChild(new Label { Text = "Stable Orbits" });
        _moons = AddLabel();
        _limits = AddLabel();
        _limits.TooltipText = "Roche limit: closer than this, its tides would tear a moon " +
            "apart (rings lie inside it). Hill sphere: how far its own pull beats the pull of " +
            "what it circles; moons stay steady out to about half of it";
        _warnings = AddLabel();
        _periodRow = new HBoxContainer();
        _period = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _period.TooltipText = "How long one trip round would take at this distance under " +
            "real gravity, from the two bodies' masses";
        _usePeriod = new Button
        {
            Text = "Use",
            FocusMode = FocusModeEnum.None,
            TooltipText = "Set the orbit's period to this",
        };
        _usePeriod.Pressed += UseNaturalPeriod;
        _periodRow.AddChild(_period);
        _periodRow.AddChild(_usePeriod);
        AddChild(_periodRow);
        Refresh();
    }

    /// <summary>Shows the guide for the selected body.</summary>
    public void Refresh()
    {
        if (!IsNodeReady())
        {
            return;
        }

        List<Body> bodies = Session.World.Bodies;
        Body body = Session.SelectedBody;
        ShowZone(bodies, body);
        ShowWarnings(bodies, body);
        ShowPeriod(bodies, body);
    }

    private void ShowZone(List<Body> bodies, Body body)
    {
        OrbitZone? zone = OrbitStability.MoonZone(bodies, body);
        string what = body.Kind is BodyKind.Star or BodyKind.WorldTree ? "Orbits" : "Moons";
        _moons.Text = zone switch
        {
            null => $"No room for steady moons: {body.Name} is too close to what it circles.",
            _ when double.IsInfinity(zone.OuterKm) =>
                $"{what} stay steady from {Distance(zone.InnerKm)} outward.",
            _ => $"{what} stay steady from {Distance(zone.InnerKm)} to {Distance(zone.OuterKm)}.",
        };
        double roche = OrbitStability.RocheLimitKm(body, OrbitStability.TypicalMoonDensity);
        double hill = OrbitStability.HillRadiusKm(bodies, body);
        _limits.Text = $"Roche limit {Distance(roche)}  ·  Hill sphere " +
            (double.IsInfinity(hill) ? "unlimited" : Distance(hill));
    }

    private void ShowWarnings(List<Body> bodies, Body body)
    {
        List<string> warnings = [.. OrbitStability.Warnings(bodies)
            .Where(w => w.BodyId == body.Id)
            .Select(w => $"⚠ {w.Message}.")];
        bool steadyOrbit = warnings.Count == 0 && body.Orbit is not null && body.Branch is null;
        _warnings.Text = steadyOrbit ? "Its orbit would stay steady." : string.Join("\n", warnings);
        _warnings.Visible = warnings.Count > 0 || steadyOrbit;
        if (warnings.Count > 0)
        {
            _warnings.AddThemeColorOverride("font_color", _warningColor);
        }
        else
        {
            _warnings.RemoveThemeColorOverride("font_color");
        }
    }

    private void ShowPeriod(List<Body> bodies, Body body)
    {
        double? natural = OrbitStability.NaturalPeriodDays(bodies, body);
        _periodRow.Visible = natural is not null;
        if (natural is not double days || body.Orbit is not Orbit orbit)
        {
            return;
        }

        _naturalPeriod = days;
        _period.Text = $"Gravity's period: {PeriodText(days)}";
        (_, Body? fittedBy) = CalendarFitting.FittedBy(bodies, body);
        _usePeriod.Disabled = fittedBy is not null || orbit.PeriodDays == days;
        _usePeriod.TooltipText = fittedBy is not null
            ? $"The period is set by {fittedBy.Name}'s calendar"
            : "Set the orbit's period to this";
    }

    private void UseNaturalPeriod()
    {
        if (Session.SelectedBody.Orbit is Orbit orbit)
        {
            ReportProblem?.Invoke(Session.SetOrbit(Session.SelectedBodyId,
                orbit with { PeriodDays = _naturalPeriod }));
        }
    }

    // Far distances in AU, middling ones in millions of km or miles, near ones in km or miles.
    private static string Distance(double km) => UnitText.Distance(km);

    private static string PeriodText(double days) => days >= 2
        ? $"{days.ToString("#,0.##", CultureInfo.CurrentCulture)} days"
        : $"{(days * 24).ToString("#,0.##", CultureInfo.CurrentCulture)} hours";

    private Label AddLabel()
    {
        var label = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(label);
        return label;
    }
}
