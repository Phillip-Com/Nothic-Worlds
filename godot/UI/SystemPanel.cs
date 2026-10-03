using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The System panel (VISION.md UI-02, BOD-01, SIM-01), on the left of the screen: the system's
/// bodies as a tree (Sun ▸ Planet ▸ Moon), buttons to add and delete bodies, and the selected
/// body's properties (name, kind, size, day, tilt, and orbit) with exact numbers.
/// </summary>
/// <remarks>
/// All edits go through <see cref="WorldSession"/>, so they're tracked as unsaved changes and can
/// be undone. Deleting a body also deletes everything orbiting it (owner decision).
/// </remarks>
public partial class SystemPanel : CanvasLayer
{
    private const int ScreenMargin = 12;
    /// <summary>The panel's width, for laying things out beside it.</summary>
    public const float PanelWidth = 330.0f;

    // Below the toolbar and its message line, above the camera mode text.
    private const int TopOffset = 104;
    private const int BottomOffset = 48;

    private const double KmPerAu = 149_597_870.7;
    private const string DayLengthTip = "How long one spin takes, in standard hours (Earth: 24)";
    private const string PeriodTip = "How long one trip around takes (Earth: 365.25 days)";

    private Tree _tree = null!;
    private Button _addMoonButton = null!;
    private Button _deleteButton = null!;
    private Button _centerButton = null!;
    private LineEdit _name = null!;
    private OptionButton _kind = null!;
    private Label _kindLabel = null!;
    private SpinBox _radius = null!;
    private SpinBox _dayLength = null!;
    private SpinBox _axialTilt = null!;
    private SpinBox _axisDirection = null!;
    private SpinBox _temperature = null!;
    private ColorPickerButton _color = null!;
    private OptionButton _pattern = null!;
    private OptionButton _starType = null!;
    private Control[] _surfaceLook = [];
    private Control[] _starLook = [];
    private Control _orbitFields = null!;
    private Label _noOrbit = null!;
    private OptionButton _parent = null!;
    private SpinBox _distance = null!;
    private OptionButton _distanceUnit = null!;
    private SpinBox _period = null!;
    private SpinBox _startAngle = null!;
    private CheckButton _extrasToggle = null!;
    private Control _extras = null!;
    private SpinBox _eccentricity = null!;
    private SpinBox _closestApproach = null!;
    private SpinBox _orbitTilt = null!;
    private SpinBox _tiltDirection = null!;
    private CalendarSection? _calendar;
    private EclipseSection? _eclipses;
    private MeteorShowerSection? _showers;
    private bool _open;
    private bool _syncing;

    // What the tree shows, to skip rebuilding it when nothing it shows has changed.
    private string _treeSignature = "";

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>Where messages go. The panel hides whenever the toolbar does.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>
    /// The system view: while the panel shows, it always draws the selected body's path,
    /// highlighted (owner's request).
    /// </summary>
    [Export] public Rendering.SystemView? System { get; set; }

    /// <summary>The time bar, for the seasons' Go to buttons.</summary>
    [Export] public TimeControls? Time { get; set; }

    private enum DistanceUnit
    {
        Kilometers,
        AstronomicalUnits,
    }

    /// <summary>Whether the panel is open (it's still hidden while the toolbar is).</summary>
    public bool IsPanelOpen
    {
        get => _open;
        set
        {
            _open = value;
            UpdateVisibility();
        }
    }

    public override void _Ready()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(PanelWidth, 0) };
        panel.AnchorTop = 0;
        panel.AnchorBottom = 1;
        panel.OffsetLeft = ScreenMargin;
        panel.OffsetRight = ScreenMargin + PanelWidth;
        panel.OffsetTop = TopOffset;
        panel.OffsetBottom = -BottomOffset;
        panel.AddThemeStyleboxOverride("panel", PanelStyle.SidePanel());
        AddChild(panel);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        panel.AddChild(scroll);
        var layout = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(layout);

        layout.AddChild(new Label { Text = "Star System" });
        _tree = new Tree
        {
            HideRoot = true,
            CustomMinimumSize = new Vector2(0, 150),
            FocusMode = Control.FocusModeEnum.None,
        };
        _tree.ItemSelected += OnTreeSelected;
        layout.AddChild(_tree);
        layout.AddChild(BuildButtons());
        layout.AddChild(new HSeparator());
        layout.AddChild(BuildProperties());

        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += UpdateVisibility;
        }

        if (Session is null)
        {
            GD.PushError("SystemPanel needs a world session.");
            return;
        }

        layout.AddChild(new HSeparator());
        _calendar = new CalendarSection { Session = Session, Time = Time };
        layout.AddChild(_calendar);
        layout.AddChild(new HSeparator());
        _eclipses = new EclipseSection { Session = Session, Time = Time };
        layout.AddChild(_eclipses);
        _showers = new MeteorShowerSection { Session = Session, Time = Time };
        layout.AddChild(_showers);

        Session.Changed += SyncWithWorld;
        Session.SelectionChanged += SyncWithWorld;
        SyncWithWorld();
        UpdateVisibility();
    }

    private Control BuildButtons()
    {
        var buttons = new HFlowContainer();
        buttons.AddChild(CreateButton("Add Planet", () => _ = AddAsync(BodyKind.Planet),
            "A planet orbiting the selected star (or the selected body's star)"));
        _addMoonButton = CreateButton("Add Moon", () => _ = AddAsync(BodyKind.Moon),
            "A moon orbiting the selected planet or moon");
        buttons.AddChild(_addMoonButton);
        buttons.AddChild(CreateButton("Add Star", () => _ = AddAsync(BodyKind.Star),
            "A companion star, far out around the system's central star"));
        buttons.AddChild(CreateButton("Add Comet", () => _ = AddAsync(BodyKind.Comet),
            "A comet on a long, elongated orbit around the selected body's star, crossing the " +
            "innermost planet's orbit"));
        _deleteButton = CreateButton("Delete", () => _ = DeleteAsync(),
            "Delete the selected body and everything orbiting it (Ctrl+Z brings them back)");
        buttons.AddChild(_deleteButton);
        _centerButton = CreateButton("Make Center", MakeCenter,
            "Put the selected body at the center of its system. The bodies it orbited circle " +
            "it instead, on the same paths, so everything stays in the same place relative " +
            "to everything else (e.g. a planet-centered system, with the sun going around it)");
        buttons.AddChild(_centerButton);
        return buttons;
    }

    private Control BuildProperties()
    {
        var layout = new VBoxContainer();
        _name = new LineEdit { PlaceholderText = "Body name" };
        _name.TextSubmitted += _ => CommitName();
        _name.FocusExited += CommitName;
        layout.AddChild(_name);

        var grid = new GridContainer { Columns = 2 };
        grid.AddChild(new Label { Text = "Kind" });
        var kindRow = new HBoxContainer();
        _kind = new Dropdown { FocusMode = Control.FocusModeEnum.None };
        _kind.AddItem("Planet", (int)BodyKind.Planet);
        _kind.AddItem("Moon", (int)BodyKind.Moon);
        _kind.ItemSelected += index => CommitKind((BodyKind)_kind.GetItemId((int)index));
        _kindLabel = new Label { Text = "Star" };
        kindRow.AddChild(_kind);
        kindRow.AddChild(_kindLabel);
        grid.AddChild(kindRow);
        _radius = AddField(grid, "Radius", 1, Body.MaxRadiusKm, 1, "km", CommitPhysical);
        _dayLength = AddField(grid, "Day length", 0.01, Body.MaxDayLengthHours, 0.1, "h",
            CommitPhysical);
        _axialTilt = AddField(grid, "Axial tilt", 0, 180, 0.1, "°", CommitPhysical);
        _radius.TooltipText = "The body's radius (Earth: 6,371 km)";
        _dayLength.TooltipText = DayLengthTip;
        _axisDirection = AddField(grid, "Axis direction", 0, 360, 1, "°", CommitPhysical)
            .WithWrapAround();
        _axialTilt.TooltipText = "How far the spin axis leans (Earth: 23.4°)";
        _axisDirection.TooltipText = "Which way the north pole leans, measured like the " +
            "orbit angles. It sets when in the year the solstices fall";
        _temperature = AddField(grid, "Avg. temperature", Body.MinAverageTemperatureC,
            Body.MaxAverageTemperatureC, 0.5, "°C", CommitTemperature);
        _temperature.TooltipText = "The body's average surface temperature over a year " +
            "(Earth: about 15 °C). Weather pins spread it by latitude and season";
        AddAppearanceFields(grid);
        layout.AddChild(grid);

        layout.AddChild(new Label { Text = "Orbit" });
        _noOrbit = new Label
        {
            Text = "At the center of the system.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        layout.AddChild(_noOrbit);
        _orbitFields = BuildOrbitFields();
        layout.AddChild(_orbitFields);
        return layout;
    }

    private Control BuildOrbitFields()
    {
        var layout = new VBoxContainer();
        var grid = new GridContainer { Columns = 2 };
        grid.AddChild(new Label { Text = "Orbits" });
        _parent = new Dropdown
        {
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _parent.ItemSelected += _ => CommitOrbit();
        grid.AddChild(_parent);

        grid.AddChild(new Label { Text = "Distance" });
        var distanceRow = new HBoxContainer();
        _distance = CreateField(0.000001, 1e13, 0.001, "", CommitOrbit);
        _distance.TooltipText = "The orbit's size: average distance, center to center";
        _distanceUnit = new Dropdown { FocusMode = Control.FocusModeEnum.None };
        _distanceUnit.AddItem("km", (int)DistanceUnit.Kilometers);
        _distanceUnit.AddItem("AU", (int)DistanceUnit.AstronomicalUnits);
        _distanceUnit.TooltipText = "AU: the Earth–Sun distance (about 149.6 million km)";
        _distanceUnit.ItemSelected += _ => ShowSelected();
        distanceRow.AddChild(_distance);
        distanceRow.AddChild(_distanceUnit);
        grid.AddChild(distanceRow);

        _period = AddField(grid, "Period", 0.0001, Orbit.MaxPeriodDays, 0.01, "days",
            CommitOrbit);
        _period.TooltipText = PeriodTip;
        _startAngle = AddField(grid, "Start angle", 0, 360, 1, "°", CommitOrbit)
            .WithWrapAround();
        _startAngle.TooltipText = "Where along the orbit the body is at day 1";
        layout.AddChild(grid);

        _extrasToggle = new CheckButton
        {
            Text = "Elongated or tilted orbit",
            FocusMode = Control.FocusModeEnum.None,
        };
        _extrasToggle.Toggled += on => _extras.Visible = on;
        layout.AddChild(_extrasToggle);

        var extras = new GridContainer { Columns = 2 };
        _eccentricity = AddField(extras, "Elongation", 0, Orbit.MaxEccentricity, 0.01, "",
            CommitOrbit);
        _eccentricity.TooltipText = "0 is a circle; up to 0.95 (eccentricity)";
        _closestApproach = AddField(extras, "Closest point", 0, 360, 1, "°", CommitOrbit)
            .WithWrapAround();
        _closestApproach.TooltipText = "Which way the elongated orbit points (closest approach)";
        _orbitTilt = AddField(extras, "Tilt", 0, 180, 0.1, "°", CommitOrbit);
        _orbitTilt.TooltipText = "0 is flat, 90 passes over the poles, over 90 runs backwards";
        _tiltDirection = AddField(extras, "Tilt direction", 0, 360, 1, "°", CommitOrbit)
            .WithWrapAround();
        _tiltDirection.TooltipText = "Where the orbit rises north through the flat plane";
        _extras = extras;
        _extras.Visible = false;
        layout.AddChild(extras);
        return layout;
    }

    // Rebuilds the tree (if bodies or names changed) and refreshes the properties.
    private void SyncWithWorld()
    {
        if (Session is null)
        {
            return;
        }

        IReadOnlyList<Body> bodies = Session.World.Bodies;
        string signature = string.Join("|", bodies.Select(b =>
            $"{b.Id}:{b.Name}:{b.Kind}:{b.Orbit?.ParentId}"));
        if (signature != _treeSignature)
        {
            RebuildTree(bodies);
            _treeSignature = signature;
        }

        SelectInTree(Session.SelectedBodyId);
        HighlightPath();
        Body selected = Session.SelectedBody;
        _addMoonButton.Disabled = !selected.HasSurface;
        _deleteButton.Disabled = SystemHierarchy.DescendantsOf(bodies, selected.Id).Count + 1
            >= bodies.Count;
        _centerButton.Disabled = selected.Orbit is null || selected.Kind == BodyKind.Comet;
        ShowSelected();
    }

    private void RebuildTree(IReadOnlyList<Body> bodies)
    {
        _syncing = true;
        _tree.Clear();
        TreeItem root = _tree.CreateItem();
        foreach (Body body in bodies.Where(b => b.Orbit is null))
        {
            AddTreeItems(root, body, bodies);
        }

        _syncing = false;
    }

    private void AddTreeItems(TreeItem parent, Body body, IReadOnlyList<Body> bodies)
    {
        TreeItem item = _tree.CreateItem(parent);
        string kind = body.Kind switch
        {
            BodyKind.Star => "star",
            BodyKind.Moon => "moon",
            BodyKind.Comet => "comet",
            _ => "planet",
        };
        item.SetText(0, $"{body.Name}  ({kind})");
        item.SetMetadata(0, body.Id.ToString());
        foreach (Body child in SystemHierarchy.ChildrenOf(bodies, body.Id))
        {
            AddTreeItems(item, child, bodies);
        }
    }

    private void SelectInTree(Guid id)
    {
        _syncing = true;
        for (TreeItem? item = _tree.GetRoot()?.GetFirstChild(); item is not null;
            item = item.GetNextInTree())
        {
            if (item.GetMetadata(0).AsString() == id.ToString())
            {
                item.Select(0);
                _tree.ScrollToItem(item);
                break;
            }
        }

        _syncing = false;
    }

    private void OnTreeSelected()
    {
        if (_syncing || Session is null || _tree.GetSelected() is not TreeItem item
            || !Guid.TryParse(item.GetMetadata(0).AsString(), out Guid id))
        {
            return;
        }

        _ = SelectAsync(id);
    }

    private async Task SelectAsync(Guid id)
    {
        if (await Session!.SelectBodyAsync(id) is string warning)
        {
            Toolbar?.ShowWarning(warning);
        }
    }

    // Fills the fields from the selected body, without triggering edits.
    private void ShowSelected()
    {
        if (Session is null)
        {
            return;
        }

        Body body = Session.SelectedBody;
        _syncing = true;
        if (!_name.HasFocus())
        {
            _name.Text = body.Name;
        }

        bool isStar = body.Kind == BodyKind.Star;
        _kind.Visible = body.HasSurface;
        _kindLabel.Visible = !body.HasSurface;
        _kindLabel.Text = isStar ? "Star" : "Comet";
        if (body.HasSurface)
        {
            _kind.Select(_kind.GetItemIndex((int)body.Kind));
        }

        _radius.ShowValue(body.RadiusKm);
        _dayLength.ShowValue(body.DayLengthHours);
        (Body? dayBy, Body? periodBy) = CalendarFitting.FittedBy(Session!.World.Bodies, body);
        Lock(_dayLength, dayBy, DayLengthTip, "keeps each year exactly one calendar year");
        _axialTilt.ShowValue(body.AxialTiltDegrees);
        _axisDirection.ShowValue(body.AxialTiltDirectionDegrees);
        _temperature.ShowValue(body.AverageTemperatureC);
        ShowAppearance(body);

        // Stars and comets have no surface weather.
        _temperature.Visible = body.HasSurface;
        _temperature.GetParent().GetChild<Control>(_temperature.GetIndex() - 1).Visible =
            body.HasSurface;

        _orbitFields.Visible = body.Orbit is not null;
        _noOrbit.Visible = body.Orbit is null;
        if (body.Orbit is Orbit orbit)
        {
            ShowOrbit(body, orbit);
            Lock(_period, periodBy, PeriodTip, periodBy?.Calendar?.MonthMoonId == body.Id
                ? $"keeps one cycle of {body.Name} per month"
                : "keeps each year exactly one calendar year");
        }

        _calendar?.Refresh();
        if (_eclipses is not null)
        {
            _eclipses.Visible = body.HasSurface;  // Stars and comets have no eclipses.
            _eclipses.Refresh();
        }

        if (_showers is not null)
        {
            _showers.Visible = body.HasSurface;  // Nor meteor showers.
            _showers.Refresh();
        }

        _syncing = false;
    }

    private void ShowOrbit(Body body, Orbit orbit)
    {
        _parent.Clear();
        foreach (Body candidate in Session!.PossibleParents(body.Id))
        {
            _parent.AddItem(candidate.Name);
            _parent.SetItemMetadata(_parent.ItemCount - 1, candidate.Id.ToString());
            if (candidate.Id == orbit.ParentId)
            {
                _parent.Select(_parent.ItemCount - 1);
            }
        }

        bool inAu = SelectedUnit() == DistanceUnit.AstronomicalUnits;
        _distance.Step = inAu ? 0.001 : 1;
        _distance.ShowValue(inAu ? orbit.DistanceKm / KmPerAu : orbit.DistanceKm);
        _period.ShowValue(orbit.PeriodDays);
        _startAngle.ShowValue(orbit.StartAngleDegrees);
        _eccentricity.ShowValue(orbit.Eccentricity);
        _closestApproach.ShowValue(orbit.ClosestApproachDegrees);
        _orbitTilt.ShowValue(orbit.TiltDegrees);
        _tiltDirection.ShowValue(orbit.TiltDirectionDegrees);

        // Extras show if this orbit uses them (or the user opened them).
        bool usesExtras = orbit.Eccentricity != 0 || orbit.TiltDegrees != 0;
        if (usesExtras && !_extrasToggle.ButtonPressed)
        {
            _extrasToggle.ButtonPressed = true;
        }
    }

    // A field set by a calendar that fits the world (VISION.md CAL-02) can't be typed in; its
    // tooltip says which calendar sets it.
    private static void Lock(SpinBox field, Body? fittedBy, string tip, string why)
    {
        field.Editable = fittedBy is null;
        field.TooltipText = fittedBy is null
            ? tip
            : $"Set by {fittedBy.Name}'s calendar, which {why}. To change it, turn that off " +
              "in the calendar editor.";
    }

    private DistanceUnit SelectedUnit() => (DistanceUnit)_distanceUnit.GetSelectedId();

    private void CommitName()
    {
        if (!_syncing && Session is not null)
        {
            Session.RenameBody(Session.SelectedBodyId, _name.Text);
            _name.Text = Session.SelectedBody.Name;
        }
    }

    private void CommitKind(BodyKind kind)
    {
        if (!_syncing)
        {
            Session?.SetBodyKind(Session.SelectedBodyId, kind);
        }
    }

    private void CommitPhysical()
    {
        if (_syncing || Session is null)
        {
            return;
        }

        string? problem = Session.SetBodyPhysical(Session.SelectedBodyId,
            _radius.Value, _dayLength.Value, _axialTilt.Value, _axisDirection.Value);
        ReportProblem(problem);
    }

    // How the body looks (VISION.md BOD-06): a planet's or moon's color and pattern, or a
    // star's type. Only the rows for the selected kind show.
    private void AddAppearanceFields(GridContainer grid)
    {
        var colorLabel = new Label { Text = "Color" };
        _color = new ColorPickerButton
        {
            EditAlpha = false,
            CustomMinimumSize = new Vector2(0, 28),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "The surface's color where there's no map",
        };
        _color.ColorChanged += _ => CommitAppearance();
        var patternLabel = new Label { Text = "Pattern" };
        _pattern = new Dropdown
        {
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "How the surface looks where there's no map",
        };
        _pattern.AddItem("Plain", (int)SurfacePattern.Plain);
        _pattern.AddItem("Rocky", (int)SurfacePattern.Rocky);
        _pattern.AddItem("Banded (gas giant)", (int)SurfacePattern.Banded);
        _pattern.AddItem("Icy", (int)SurfacePattern.Icy);
        _pattern.AddItem("Cloudy", (int)SurfacePattern.Cloudy);
        _pattern.ItemSelected += _ => CommitAppearance();
        var starLabel = new Label { Text = "Star type" };
        _starType = new Dropdown
        {
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Sets the star's color and the color of its light",
        };
        _starType.AddItem("Red dwarf", (int)StarType.RedDwarf);
        _starType.AddItem("Orange", (int)StarType.Orange);
        _starType.AddItem("Yellow (Sun-like)", (int)StarType.Yellow);
        _starType.AddItem("White", (int)StarType.White);
        _starType.AddItem("Blue", (int)StarType.Blue);
        _starType.ItemSelected += _ => CommitAppearance();
        foreach (Control control in new Control[]
            { colorLabel, _color, patternLabel, _pattern, starLabel, _starType })
        {
            grid.AddChild(control);
        }

        _surfaceLook = [colorLabel, _color, patternLabel, _pattern];
        _starLook = [starLabel, _starType];
    }

    private void ShowAppearance(Body body)
    {
        bool isStar = body.Kind == BodyKind.Star;
        foreach (Control control in _surfaceLook)
        {
            control.Visible = !isStar;
        }

        foreach (Control control in _starLook)
        {
            control.Visible = isStar;
        }

        _color.Color = body.Appearance.Color.ToGodot();
        _pattern.Select(_pattern.GetItemIndex((int)body.Appearance.Pattern));
        _starType.Select(_starType.GetItemIndex((int)body.Appearance.StarType));
    }

    private void CommitAppearance()
    {
        if (_syncing || Session is null)
        {
            return;
        }

        Session.SetAppearance(Session.SelectedBodyId, Session.SelectedBody.Appearance with
        {
            Color = _color.Color.ToRgbColor(),
            Pattern = (SurfacePattern)_pattern.GetSelectedId(),
            StarType = (StarType)_starType.GetSelectedId(),
        });
    }

    private void CommitTemperature()
    {
        if (!_syncing && Session is not null)
        {
            ReportProblem(
                Session.SetAverageTemperature(Session.SelectedBodyId, _temperature.Value));
        }
    }

    private void CommitOrbit()
    {
        if (_syncing || Session is null || Session.SelectedBody.Orbit is not Orbit current
            || _parent.Selected < 0
            || !Guid.TryParse(
                _parent.GetItemMetadata(_parent.Selected).AsString(), out Guid parent))
        {
            return;
        }

        double distanceKm = SelectedUnit() == DistanceUnit.AstronomicalUnits
            ? _distance.Value * KmPerAu
            : _distance.Value;
        Orbit orbit = current with
        {
            ParentId = parent,
            DistanceKm = distanceKm,
            PeriodDays = _period.Value,
            StartAngleDegrees = _startAngle.Value,
            Eccentricity = _eccentricity.Value,
            ClosestApproachDegrees = _closestApproach.Value,
            TiltDegrees = _orbitTilt.Value,
            TiltDirectionDegrees = _tiltDirection.Value,
        };
        ReportProblem(Session.SetOrbit(Session.SelectedBodyId, orbit));
    }

    // Shows why an edit was refused, and puts the fields back to the body's real values. While
    // the user is still typing, half-typed numbers just don't apply yet, without a message.
    private void ReportProblem(string? problem)
    {
        if (problem is not null && GetViewport().GuiGetFocusOwner() is not LineEdit)
        {
            Toolbar?.ShowError($"Can't use that: {problem}.");
            ShowSelected();
        }
    }

    /// <summary>
    /// Adds a body with starting values (see <see cref="WorldSession.AddBodyAsync"/>) and shows
    /// it here.
    /// </summary>
    public async Task AddAsync(BodyKind kind)
    {
        if (Session is null || await Session.AddBodyAsync(kind) is not Body body)
        {
            return;
        }

        // Moons default to km, everything around a star to AU.
        bool aroundStar = body.Orbit is Orbit orbit
            && Session.World.Bodies.Find(b => b.Id == orbit.ParentId)?.Kind == BodyKind.Star;
        _distanceUnit.Select(_distanceUnit.GetItemIndex(
            (int)(aroundStar ? DistanceUnit.AstronomicalUnits : DistanceUnit.Kilometers)));
        ShowSelected();
        Toolbar?.ShowInfo($"Added {body.Name}. Set its size, day, and orbit here.");
    }

    private void MakeCenter()
    {
        if (Session is not null && Session.MakeCenter(Session.SelectedBodyId))
        {
            Toolbar?.ShowInfo($"{Session.SelectedBody.Name} is now the center of its system " +
                "(Ctrl+Z to undo).");
        }
    }

    // No confirmation: deleting can be undone (owner decision).
    private async Task DeleteAsync()
    {
        if (Session is null)
        {
            return;
        }

        if (await Session.RemoveBodyAsync(Session.SelectedBodyId) is string what)
        {
            Toolbar?.ShowInfo($"Deleted {what} (Ctrl+Z to undo).");
        }
    }

    private void UpdateVisibility()
    {
        Visible = _open && (Toolbar?.Visible ?? true);
        HighlightPath();
    }

    // While editing, the selected body's path always shows, highlighted.
    private void HighlightPath()
    {
        if (System is not null)
        {
            System.HighlightedOrbit = Visible ? Session?.SelectedBodyId : null;
        }
    }

    private SpinBox AddField(GridContainer grid, string label, double min, double max,
        double step, string suffix, Action changed)
    {
        grid.AddChild(new Label { Text = label });
        SpinBox field = CreateField(min, max, step, suffix, changed);
        grid.AddChild(field);
        return field;
    }

    private SpinBox CreateField(double min, double max, double step, string suffix,
        Action changed)
    {
        var field = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Suffix = suffix,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        field.WithLiveTyping(ShowSelected).ValueChanged += _ => changed();
        return field;
    }

    private static Button CreateButton(string text, Action pressed, string tooltip)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.None,
        };
        button.Pressed += pressed;
        return button;
    }
}
