using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
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
    private const float PanelWidth = 330.0f;

    // Below the toolbar and its message line, above the camera mode text.
    private const int TopOffset = 104;
    private const int BottomOffset = 48;

    private const double KmPerAu = 149_597_870.7;

    private Tree _tree = null!;
    private Button _addMoonButton = null!;
    private Button _deleteButton = null!;
    private LineEdit _name = null!;
    private OptionButton _kind = null!;
    private Label _kindLabel = null!;
    private SpinBox _radius = null!;
    private SpinBox _dayLength = null!;
    private SpinBox _axialTilt = null!;
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
    private bool _open;
    private bool _syncing;

    // What the tree shows, to skip rebuilding it when nothing it shows has changed.
    private string _treeSignature = "";

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>Where messages go. The panel hides whenever the toolbar does.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

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
        _deleteButton = CreateButton("Delete", () => _ = DeleteAsync(),
            "Delete the selected body and everything orbiting it (Ctrl+Z brings them back)");
        buttons.AddChild(_deleteButton);
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
        _kind = new OptionButton { FocusMode = Control.FocusModeEnum.None };
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
        _dayLength.TooltipText = "How long one spin takes, in standard hours (Earth: 24)";
        _axialTilt.TooltipText = "How far the spin axis leans (Earth: 23.4°)";
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
        _parent = new OptionButton
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
        _distanceUnit = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _distanceUnit.AddItem("km", (int)DistanceUnit.Kilometers);
        _distanceUnit.AddItem("AU", (int)DistanceUnit.AstronomicalUnits);
        _distanceUnit.TooltipText = "AU: the Earth–Sun distance (about 149.6 million km)";
        _distanceUnit.ItemSelected += _ => ShowSelected();
        distanceRow.AddChild(_distance);
        distanceRow.AddChild(_distanceUnit);
        grid.AddChild(distanceRow);

        _period = AddField(grid, "Period", 0.0001, Orbit.MaxPeriodDays, 0.01, "days",
            CommitOrbit);
        _period.TooltipText = "How long one trip around takes (Earth: 365.25 days)";
        _startAngle = AddField(grid, "Start angle", -360, 360, 1, "°", CommitOrbit);
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
        _closestApproach = AddField(extras, "Closest point", -360, 360, 1, "°", CommitOrbit);
        _closestApproach.TooltipText = "Which way the elongated orbit points (closest approach)";
        _orbitTilt = AddField(extras, "Tilt", 0, 180, 0.1, "°", CommitOrbit);
        _orbitTilt.TooltipText = "0 is flat, 90 passes over the poles, over 90 runs backwards";
        _tiltDirection = AddField(extras, "Tilt direction", -360, 360, 1, "°", CommitOrbit);
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
        Body selected = Session.SelectedBody;
        _addMoonButton.Disabled = selected.Kind == BodyKind.Star;
        _deleteButton.Disabled = SystemHierarchy.DescendantsOf(bodies, selected.Id).Count + 1
            >= bodies.Count;
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
        _kind.Visible = !isStar;
        _kindLabel.Visible = isStar;
        if (!isStar)
        {
            _kind.Select(_kind.GetItemIndex((int)body.Kind));
        }

        _radius.SetValueNoSignal(body.RadiusKm);
        _dayLength.SetValueNoSignal(body.DayLengthHours);
        _axialTilt.SetValueNoSignal(body.AxialTiltDegrees);

        _orbitFields.Visible = body.Orbit is not null;
        _noOrbit.Visible = body.Orbit is null;
        if (body.Orbit is Orbit orbit)
        {
            ShowOrbit(body, orbit);
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
        _distance.SetValueNoSignal(inAu ? orbit.DistanceKm / KmPerAu : orbit.DistanceKm);
        _period.SetValueNoSignal(orbit.PeriodDays);
        _startAngle.SetValueNoSignal(orbit.StartAngleDegrees);
        _eccentricity.SetValueNoSignal(orbit.Eccentricity);
        _closestApproach.SetValueNoSignal(orbit.ClosestApproachDegrees);
        _orbitTilt.SetValueNoSignal(orbit.TiltDegrees);
        _tiltDirection.SetValueNoSignal(orbit.TiltDirectionDegrees);

        // Extras show if this orbit uses them (or the user opened them).
        bool usesExtras = orbit.Eccentricity != 0 || orbit.TiltDegrees != 0;
        if (usesExtras && !_extrasToggle.ButtonPressed)
        {
            _extrasToggle.ButtonPressed = true;
        }
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
            _radius.Value, _dayLength.Value, _axialTilt.Value);
        ReportProblem(problem);
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

    // Shows why an edit was refused, and puts the fields back to the body's real values.
    private void ReportProblem(string? problem)
    {
        if (problem is not null)
        {
            Toolbar?.ShowError($"Can't use that: {problem}.");
            ShowSelected();
        }
    }

    private async Task AddAsync(BodyKind kind)
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
        field.WithArrowKeys().ValueChanged += _ => changed();
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
