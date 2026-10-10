using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Measurement;
using NothicWorlds.Core.Model;
using NothicWorlds.Interop;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The Terrain panel (VISION.md BOD-05; owner's choice: its own panel, one at a time with the
/// others on the right): Paint, Erase, Sculpt, Shapes (BOD-04; owner's choice: here), or Water
/// (BOD-11: rivers and lakes, <see cref="WaterSection"/>), the brush size, the sculpting brushes
/// and their strength, the shapes (<see cref="ShapesSection"/>), and the world's terrain types,
/// which can be added, renamed, recolored, and deleted. While
/// it's open, dragging on the selected planet or moon paints with the selected type or sculpts
/// it (<see cref="TerrainBrush"/>), or, in Shapes mode, clicking places a shape and its handles
/// shape it (<see cref="ShapeHandles"/>). The tool switch (or P) turns that off, so drags turn the
/// view instead.
/// </summary>
public partial class TerrainPanel : CanvasLayer
{
    private const int ScreenMargin = 12;
    private const int TopOffset = 56;
    private const int BottomOffset = 130;
    private const float PanelWidth = 300.0f;

    // Brush radius limits, in degrees of arc: from about one cell to a quarter of the globe.
    private const double MinRadiusDegrees = 0.05;
    private const double MaxRadiusDegrees = 45;

    // The Roughness row's tooltip, and why it's off for a level type (VISION.md BOD-12).
    private const string RoughnessTip = "How rough this terrain is up close, standing on it: " +
        "its variation carried on into crags, ridges, and bumps too small for the terrain " +
        "grid (0%: smooth, 50%: natural, 100%: craggy). Seen only up close";

    private const string RoughnessNeedsVariation = "Give this terrain some Variation first: " +
        "roughness carries its rises and falls on into smaller ones up close";

    // How much Raise and Lower move the ground per stroke, in meters, and how far Smooth and
    // Flatten go, in percent.
    private const double MinHeightMeters = 10;
    private const double MaxHeightMeters = 10_000;
    private const double MinAmountPercent = 5;

    private Label _heading = null!;
    private Label _note = null!;
    private Control _tools = null!;
    private ToolSwitch _toolSwitch = null!;
    private Button _paintButton = null!;
    private Button _eraseButton = null!;
    private Button _sculptButton = null!;
    private Button _shapesButton = null!;
    private Button _waterButton = null!;
    private WaterSection? _water;
    private Control _brushSize = null!;
    private ShapesSection? _shapes;
    private Control _sculptTools = null!;
    private readonly Dictionary<SculptTool, Button> _sculptButtons = [];
    private Control _heightRow = null!;
    private HSlider _heightSlider = null!;
    private Label _heightValue = null!;
    private Control _amountRow = null!;
    private HSlider _amountSlider = null!;
    private Label _amountValue = null!;
    private HSlider _sizeSlider = null!;
    private SpinBox _sizeField = null!;
    private Tree _list = null!;
    private bool _showingList;
    private Button _deleteButton = null!;
    private LineEdit _name = null!;
    private ColorPickerButton _color = null!;
    private OptionButton _climate = null!;
    private OptionButton _ground = null!;
    private OptionButton _plants = null!;
    private CheckButton _shapesGround = null!;
    private SpinBox _height = null!;
    private HSlider _edge = null!;
    private Label _edgeName = null!;
    private SpinBox _variation = null!;
    private SpinBox _featureSize = null!;
    private HBoxContainer _roughnessRow = null!;
    private HSlider _roughness = null!;
    private Label _roughnessName = null!;
    private bool _roughnessDragging;
    private Label _shapingNote = null!;
    private bool _edgeDragging;
    private Label _problem = null!;
    private readonly Dictionary<RgbColor, ImageTexture> _swatches = [];
    private byte? _selectedCode;
    private bool _open;
    private bool _syncing;
    private string _listSignature = "";

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The toolbar: the panel hides whenever it does.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>The brush, which paints while the panel shows.</summary>
    [Export] public TerrainBrush? Brush { get; set; }

    /// <summary>The handles that place and shape shapes, in Shapes mode.</summary>
    [Export] public ShapeHandles? Shapes { get; set; }

    /// <summary>Takes the click that places a lake or a natural river, in Water mode.</summary>
    [Export] public PinPlacer? Placer { get; set; }

    /// <summary>Draws a drawn river's points, in Water mode.</summary>
    [Export] public RegionEditor? LineDrawer { get; set; }

    /// <summary>Draws the rivers, the one selected in Water mode lighter.</summary>
    [Export] public RiverRenderer? Rivers { get; set; }

    /// <summary>
    /// The live weather, whose clouds hide while the panel is open (owner's choice), so the
    /// ground shows.
    /// </summary>
    [Export] public WeatherDisplay? Weather { get; set; }

    /// <summary>Whether the panel is open (it's still hidden while the toolbar is).</summary>
    public bool IsPanelOpen
    {
        get => _open;
        set
        {
            _open = value;
            Weather?.SetSurfaceEditing("Terrain", value);
            UpdateVisibility();
            UpdateWater();
            ShowHint();
        }
    }

    public override void _Ready()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(PanelWidth, 0) };
        panel.AnchorLeft = 1;
        panel.AnchorRight = 1;
        panel.AnchorTop = 0;
        panel.AnchorBottom = 1;
        panel.OffsetLeft = -ScreenMargin - PanelWidth;
        panel.OffsetRight = -ScreenMargin;
        panel.OffsetTop = TopOffset;
        panel.OffsetBottom = -BottomOffset;
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.AddThemeStyleboxOverride("panel", PanelStyle.SidePanel());
        AddChild(panel);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        panel.AddChild(scroll);
        var layout = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        scroll.AddChild(layout);
        PanelStyle.FitHeight(panel, layout, TopOffset, BottomOffset);

        _heading = new Label();
        layout.AddChild(_heading);
        _note = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        layout.AddChild(_note);
        _tools = BuildTools();
        layout.AddChild(_tools);
        layout.AddChild(new HSeparator());
        layout.AddChild(new Label { Text = "Terrain Types" });
        _shapesGround = new CheckButton
        {
            Text = "Terrain shapes the ground",
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Painting a type also sets the ground to its height (mountains rise, " +
                "oceans sink), merging where types meet; sculpting adds on top",
        };
        _shapesGround.Toggled += on => Session?.SetTerrainShapesGround(on);
        layout.AddChild(_shapesGround);
        layout.AddChild(BuildTypeList());
        layout.AddChild(BuildTypeEditor());

        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += UpdateVisibility;
        }

        if (Session is null)
        {
            GD.PushError("TerrainPanel needs a world session.");
            return;
        }

        Session.Changed += Refresh;
        Session.SelectionChanged += Refresh;
        AppSettings.UnitsChanged += () =>
        {
            ShowSize();
            UpdateBrush();
        };
        Refresh();
        UpdateVisibility();
    }

    // Paint or Erase, and the brush size.
    private Control BuildTools()
    {
        var tools = new VBoxContainer();
        _toolSwitch = new ToolSwitch();
        _toolSwitch.Toggled += _ => Refresh();
        tools.AddChild(_toolSwitch);
        var modes = new HBoxContainer();
        var group = new ButtonGroup();
        _paintButton = CreateButton("Paint", Refresh, "Paint with the selected type");
        _eraseButton = CreateButton("Erase", Refresh, "Remove painted terrain");
        _sculptButton = CreateButton("Sculpt", Refresh,
            "Shape the ground: raise, lower, smooth, or flatten it");
        _shapesButton = CreateButton("Shapes", Refresh,
            "Add or cut spheres, boxes, cylinders, and cones: holes, hollows, craters, structures");
        _waterButton = CreateButton("Water", Refresh,
            "Add lakes and rivers: drawn, or running downhill to the sea");
        foreach (Button button in
            new[] { _paintButton, _eraseButton, _sculptButton, _shapesButton, _waterButton })
        {
            button.ToggleMode = true;
            button.ButtonGroup = group;
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            modes.AddChild(button);
        }

        _paintButton.ButtonPressed = true;
        tools.AddChild(modes);

        var sizeRow = new HBoxContainer();
        sizeRow.AddChild(new Label { Text = "Brush" });
        _sizeSlider = new HSlider
        {
            ExpEdit = true,
            MinValue = MinRadiusDegrees,
            MaxValue = MaxRadiusDegrees,
            Step = 0,
            Value = 2.0,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "The brush's radius",
        };
        _sizeSlider.ValueChanged += _ => SizeChanged(fromSlider: true);
        sizeRow.AddChild(_sizeSlider);
        _sizeField = new SpinBox
        {
            Step = 1,
            CustomMinimumSize = new Vector2(100, 0),
            TooltipText = "The brush's radius on the selected body",
        };
        _sizeField.WithLiveTyping(ShowSize).ValueChanged += _ => SizeChanged(fromSlider: false);
        sizeRow.AddChild(_sizeField);
        tools.AddChild(sizeRow);
        _brushSize = sizeRow;
        _sculptTools = BuildSculptTools();
        tools.AddChild(_sculptTools);
        if (Session is not null && Shapes is not null)
        {
            _shapes = new ShapesSection { Session = Session, Handles = Shapes, Toolbar = Toolbar };
            tools.AddChild(_shapes);
        }

        if (Session is not null)
        {
            _water = new WaterSection
            {
                Session = Session,
                Placer = Placer,
                LineDrawer = LineDrawer,
                Rivers = Rivers,
                Toolbar = Toolbar,
            };
            _water.HintChanged += ShowHint;
            tools.AddChild(_water);
        }

        return tools;
    }

    // The sculpting brushes (VISION.md BOD-04) and how strongly they work.
    private Control BuildSculptTools()
    {
        var box = new VBoxContainer();
        var brushes = new HBoxContainer();
        var group = new ButtonGroup();
        foreach ((SculptTool tool, string tip) in new[]
        {
            (SculptTool.Raise, "Push the ground up"),
            (SculptTool.Lower, "Push the ground down"),
            (SculptTool.Smooth, "Even out bumps"),
            (SculptTool.Flatten, "Level the ground to where the stroke begins"),
        })
        {
            Button button = CreateButton(tool.ToString(), Refresh, tip);
            button.ToggleMode = true;
            button.ButtonGroup = group;
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            brushes.AddChild(button);
            _sculptButtons[tool] = button;
        }

        _sculptButtons[SculptTool.Raise].ButtonPressed = true;
        box.AddChild(brushes);
        (_heightRow, _heightSlider, _heightValue) = StrengthRow("Height", MinHeightMeters,
            MaxHeightMeters, 500, "How far each stroke raises or lowers the ground");
        box.AddChild(_heightRow);
        (_amountRow, _amountSlider, _amountValue) = StrengthRow("Amount", MinAmountPercent, 100,
            50, "How much of the way each stroke goes");
        box.AddChild(_amountRow);
        return box;
    }

    private (Control Row, HSlider Slider, Label Value) StrengthRow(
        string name, double min, double max, double start, string tip)
    {
        var row = new HBoxContainer { TooltipText = tip };
        row.AddChild(new Label { Text = name });
        var slider = new HSlider
        {
            ExpEdit = true,
            MinValue = min,
            MaxValue = max,
            Step = 0,
            Value = start,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = tip,
        };
        var value = new Label { CustomMinimumSize = new Vector2(70, 0) };
        slider.ValueChanged += _ => UpdateBrush();
        row.AddChild(slider);
        row.AddChild(value);
        return (row, slider, value);
    }

    private Control BuildTypeList()
    {
        var box = new VBoxContainer();
        // A tree rather than a plain list, for the eye on each row (as in layer lists).
        _list = new Tree
        {
            CustomMinimumSize = new Vector2(0, 200),
            FocusMode = Control.FocusModeEnum.None,
            HideRoot = true,
            SelectMode = Tree.SelectModeEnum.Row,
        };
        // The tree can't be rebuilt while it handles a click, so the changes wait till after.
        _list.ButtonClicked += (item, _, _, _) =>
        {
            byte code = (byte)item.GetMetadata(0).AsInt32();
            Callable.From(() => ToggleHidden(code)).CallDeferred();
        };
        _list.ItemSelected += () =>
        {
            if (_showingList || _list.GetSelected() is not TreeItem item)
            {
                return;
            }

            byte code = (byte)item.GetMetadata(0).AsInt32();
            Callable.From(() => ChooseType(code)).CallDeferred();
        };
        box.AddChild(_list);

        var buttons = new HBoxContainer();
        buttons.AddChild(CreateButton("New Type", AddType, "Add a terrain type to this world"));
        _deleteButton = CreateButton("Delete", DeleteType,
            "Delete the selected type and clear it wherever it's painted (Ctrl+Z brings it back)");
        buttons.AddChild(_deleteButton);
        box.AddChild(buttons);
        return box;
    }

    private Control BuildTypeEditor()
    {
        var box = new VBoxContainer();
        var row = new HBoxContainer();
        _name = new LineEdit
        {
            PlaceholderText = "Name",
            MaxLength = TerrainType.MaxNameLength,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _name.TextChanged += _ => Commit();
        row.AddChild(_name);
        _color = new ColorPickerButton
        {
            CustomMinimumSize = new Vector2(36, 0),
            EditAlpha = false,
            TooltipText = "How this terrain is drawn",
        };
        _color.ColorChanged += _ => Commit();
        row.AddChild(_color);
        box.AddChild(row);

        var climateRow = new HBoxContainer();
        climateRow.AddChild(new Label { Text = "Climate" });
        _climate = new Dropdown
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "How this terrain affects the weather at weather pins",
        };
        foreach (ClimateKind kind in Enum.GetValues<ClimateKind>())
        {
            _climate.AddItem(ClimateText.Name(kind), (int)kind);
            _climate.SetItemTooltip(_climate.ItemCount - 1, ClimateText.Effect(kind));
        }

        _climate.ItemSelected += _ => Commit();
        climateRow.AddChild(_climate);
        box.AddChild(climateRow);

        var groundRow = new HBoxContainer();
        groundRow.AddChild(new Label { Text = "Ground" });
        _ground = new Dropdown
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "What this terrain's ground looks like up close while standing " +
                "(tinted toward its color); under water, the bottom",
        };
        foreach (GroundKind kind in Enum.GetValues<GroundKind>())
        {
            _ground.AddItem(GroundText.Name(kind), (int)kind);
            _ground.SetItemTooltip(_ground.ItemCount - 1, GroundText.Use(kind));
        }

        _ground.ItemSelected += _ => Commit();
        groundRow.AddChild(_ground);
        box.AddChild(groundRow);

        var plantsRow = new HBoxContainer();
        plantsRow.AddChild(new Label { Text = "Plants" });
        _plants = new Dropdown
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "What grows on this terrain while standing: trees, bushes, grass, " +
                "and rocks",
        };
        foreach (PlantCover cover in Enum.GetValues<PlantCover>())
        {
            _plants.AddItem(PlantText.Name(cover), (int)cover);
            _plants.SetItemTooltip(_plants.ItemCount - 1, PlantText.Use(cover));
        }

        _plants.ItemSelected += _ => Commit();
        plantsRow.AddChild(_plants);
        box.AddChild(plantsRow);
        box.AddChild(BuildShapingRows());
        _problem = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1.0f, 0.55f, 0.5f),
        };
        box.AddChild(_problem);
        return box;
    }

    private TerrainType? Selected =>
        Session?.TerrainTypes.FirstOrDefault(type => type.Code == _selectedCode);

    private void AddType()
    {
        if (Session?.AddTerrainType() is TerrainType type)
        {
            _selectedCode = type.Code;
            _paintButton.ButtonPressed = true;
            Refresh();
            ShowSelected(force: true);
        }
        else
        {
            Toolbar?.ShowWarning($"A world can have up to {TerrainType.MaxCount} terrain types.");
        }
    }

    private void DeleteType()
    {
        if (Session is not null && Selected is TerrainType type)
        {
            _selectedCode = null;
            Session.DeleteTerrainType(type.Code);
            Toolbar?.ShowInfo($"Deleted {type.Name} (Ctrl+Z to undo).");
        }
    }

    // The type's height, edge, and variation, for when the terrain shapes the ground
    // (VISION.md BOD-07, BOD-08).
    private Control BuildShapingRows()
    {
        var box = new VBoxContainer();
        var heightRow = new HBoxContainer();
        heightRow.AddChild(new Label { Text = "Height" });
        _height = new SpinBox
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "How high the ground is where this terrain is painted (negative: " +
                "below the planet's radius, as for seas)",
        }.WithUnit(Quantity.Length, TerrainType.MinHeightMeters, TerrainType.MaxHeightMeters,
            10, 50);
        _height.ValueChanged += _ => Commit();
        heightRow.AddChild(_height);
        box.AddChild(heightRow);

        var edgeRow = new HBoxContainer
        {
            TooltipText = "How this terrain meets others: long gentle slopes, steeper ones, or " +
                "a sheer cliff. Where two meet, the steeper edge wins.",
        };
        edgeRow.AddChild(new Label { Text = "Edge" });
        _edge = new HSlider
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.05,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            FocusMode = Control.FocusModeEnum.None,
        };

        // Re-shaping a whole planet can take a moment, so a drag applies when it's let go.
        _edge.DragStarted += () => _edgeDragging = true;
        _edge.DragEnded += _ =>
        {
            _edgeDragging = false;
            Commit();
        };
        _edge.ValueChanged += value =>
        {
            _edgeName.Text = EdgeName(value);
            if (!_edgeDragging)
            {
                Commit();
            }
        };
        edgeRow.AddChild(_edge);
        _edgeName = new Label { CustomMinimumSize = new Vector2(70, 0) };
        edgeRow.AddChild(_edgeName);
        box.AddChild(edgeRow);
        box.AddChild(BuildVariationRow());
        box.AddChild(BuildRoughnessRow());

        _shapingNote = new Label
        {
            Text = "These raise the ground once \"Terrain shapes the ground\" is on.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 1, 1, 0.6f),
        };
        box.AddChild(_shapingNote);
        return box;
    }

    // How far the ground rises and falls within the type, and how far apart its peaks are.
    private HBoxContainer BuildVariationRow()
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = "Variation" });
        _variation = new SpinBox
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "How far the ground rises and falls within this terrain, around its " +
                "height: peaks and valleys in mountains, rolling hills, a gently uneven plain " +
                "(0: level)",
        }.WithUnit(Quantity.Length, 0, TerrainType.MaxVariationMeters, 10, 50);
        _variation.ValueChanged += _ => Commit();
        row.AddChild(_variation);
        row.AddChild(new Label { Text = "Size" });
        _featureSize = new SpinBox
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "How far apart the biggest peaks and valleys are. Features smaller " +
                "than a few of the terrain grid's cells (about 10 km on an Earth-sized world) " +
                "are smoothed out",
        }.WithUnit(Quantity.Distance, TerrainType.MinFeatureSizeKm,
            TerrainType.MaxFeatureSizeKm, 1);
        _featureSize.ValueChanged += _ => Commit();
        row.AddChild(_featureSize);
        return row;
    }

    // How rough the type's ground is up close (VISION.md BOD-12): its variation carried on
    // into crags and bumps too small for the terrain grid.
    private HBoxContainer BuildRoughnessRow()
    {
        _roughnessRow = new HBoxContainer { TooltipText = RoughnessTip };
        _roughnessRow.AddChild(new Label { Text = "Roughness" });
        _roughness = new HSlider
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.05,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            FocusMode = Control.FocusModeEnum.None,
        };

        // As with the edge, a drag applies when it's let go.
        _roughness.DragStarted += () => _roughnessDragging = true;
        _roughness.DragEnded += _ =>
        {
            _roughnessDragging = false;
            Commit();
        };
        _roughness.ValueChanged += value =>
        {
            _roughnessName.Text = RoughnessName(value);
            if (!_roughnessDragging)
            {
                Commit();
            }
        };
        _roughnessRow.AddChild(_roughness);
        _roughnessName = new Label { CustomMinimumSize = new Vector2(70, 0) };
        _roughnessRow.AddChild(_roughnessName);
        return _roughnessRow;
    }

    // What a roughness setting is called: smooth, or how rough in percent.
    private static string RoughnessName(double roughness) =>
        roughness <= 0 ? "Smooth" : $"{roughness:P0}";

    // What an edge setting is called: from gentle slopes to a cliff.
    private static string EdgeName(double edge) => edge switch
    {
        < 0.2 => "Gentle",
        < 0.45 => "Moderate",
        < 0.75 => "Steep",
        < 0.95 => "Very steep",
        _ => "Cliff",
    };

    private void Commit()
    {
        if (_syncing || Session is null || Selected is not TerrainType type)
        {
            return;
        }

        string? problem = Session.UpdateTerrainType(type with
        {
            Name = _name.Text,
            Color = _color.Color.ToRgbColor(),
            Climate = (ClimateKind)_climate.GetSelectedId(),
            Ground = (GroundKind)_ground.GetSelectedId(),
            Plants = (PlantCover)_plants.GetSelectedId(),
            HeightMeters = (int)Math.Round(_height.MetricValue()),
            Edge = Math.Round(_edge.Value, 2),
            VariationMeters = (int)Math.Round(_variation.MetricValue()),
            FeatureSizeKm = Math.Round(_featureSize.MetricValue(), 1),
            Roughness = Math.Round(_roughness.Value, 2),
        });
        _problem.Text = problem is null ? "" : $"Not saved yet: {problem}.";
    }

    private void Refresh()
    {
        if (Session is null)
        {
            return;
        }

        if (Selected is null)
        {
            _selectedCode = Session.TerrainTypes.FirstOrDefault()?.Code;
        }

        Body body = Session.SelectedBody;
        bool canPaint = body.HasSurface;
        _heading.Text = $"Terrain on {body.Name}";
        _note.Text = !canPaint ? "Stars and comets can't be painted. Select a planet or moon."
            : "";
        _note.Visible = _note.Text != "";
        _tools.Visible = canPaint;
        _sculptTools.Visible = _sculptButton.ButtonPressed;
        _brushSize.Visible = !_shapesButton.ButtonPressed && !_waterButton.ButtonPressed;
        ShowToolSwitch();
        if (_shapes is not null)
        {
            _shapes.Visible = _shapesButton.ButtonPressed && canPaint;
        }

        UpdateWater();
        _shapesGround.SetPressedNoSignal(Session.TerrainShapesGround);
        _shapingNote.Visible = !Session.TerrainShapesGround;
        ShowList();
        ShowSelected(force: false);
        ShowSize();
        UpdateBrush();
        ShowHint();
    }

    private void ShowHint()
    {
        Toolbar?.SetHint(this, _open ? Hint() : null);
    }

    // How to use the mode that's chosen, for the hint bar (VISION.md UI-05).
    private string Hint()
    {
        if (!Session!.SelectedBodyHasSurface)
        {
            return "Select a planet or moon to paint it: click it, or choose it in the System " +
                "panel.";
        }

        if (_waterButton.ButtonPressed && _water is not null)
        {
            return _water.Hint();
        }

        if (!_toolSwitch.ButtonPressed)
        {
            return ToolSwitch.OffHint;
        }

        if (_shapesButton.ButtonPressed)
        {
            return "Choose a kind of shape, then click the planet to place one. Drag its middle " +
                "to move it, its square to resize it, and its round handle to turn it.";
        }

        if (_sculptButton.ButtonPressed)
        {
            return "Drag across the planet to shape the ground. Heights are true to scale, so " +
                "they show best up close: zoom in, raise View ▸ Relief, or View ▸ Stand " +
                "Here.";
        }

        if (_eraseButton.ButtonPressed)
        {
            return "Drag across the planet to take painted terrain off it.";
        }

        string name = Selected?.Name ?? "the chosen type";
        if (Selected is TerrainType selected && Session.IsTerrainHidden(selected.Code))
        {
            return $"{name} is hidden: painting it still works but won't show. Click its eye " +
                "in the list to show it.";
        }

        return Session.TerrainShapesGround
            ? $"Drag across the planet to paint {name}; the ground rises or sinks to its " +
                "height, merging where types meet. Heights are true to scale: raise View ▸ " +
                "Relief to see them from afar."
            : $"Drag across the planet to paint {name}; choose another type in the list. " +
                "Drag off the planet to turn the view.";
    }

    private void ShowList()
    {
        IReadOnlyList<TerrainType> types = Session!.TerrainTypes;
        string signature = string.Join("|", types.Select(t =>
                $"{t.Code}:{t.Name}:{t.Color}:{Session.IsTerrainHidden(t.Code)}"))
            + $"#{_selectedCode}";
        if (signature == _listSignature)
        {
            return;
        }

        _listSignature = signature;
        _showingList = true;  // Selecting a row here isn't the user choosing it
        _list.Clear();
        TreeItem root = _list.CreateItem();
        foreach (TerrainType type in types)
        {
            bool hidden = Session.IsTerrainHidden(type.Code);
            TreeItem item = _list.CreateItem(root);
            item.SetText(0, type.Name);
            item.SetIcon(0, Swatch(type.Color));
            item.SetMetadata(0, (int)type.Code);
            item.AddButton(0, EyeIcon.Get(open: !hidden), 0, false,
                hidden ? $"Show {type.Name}" : $"Hide {type.Name} (painting it still works)");
            if (hidden)
            {
                item.SetCustomColor(0, new Color(1, 1, 1, 0.45f));
            }

            if (type.Code == _selectedCode)
            {
                item.Select(0);
                _list.ScrollToItem(item);
            }
        }

        _showingList = false;
    }

    // A row in the type list was clicked: paint with that type.
    private void ChooseType(byte code)
    {
        _selectedCode = code;
        _paintButton.ButtonPressed = true;
        _problem.Text = "";
        Refresh();
    }

    // An eye in the type list was clicked: hides or shows that type.
    private void ToggleHidden(byte code)
    {
        Session!.SetTerrainHidden(code, !Session.IsTerrainHidden(code));
        Refresh();
    }

    // Fills the editor from the selected type, leaving the name alone while it's being typed.
    private void ShowSelected(bool force)
    {
        TerrainType? type = Selected;
        _deleteButton.Disabled = type is null;
        _name.Editable = type is not null;
        _color.Disabled = type is null;
        _climate.Disabled = type is null;
        _ground.Disabled = type is null;
        _plants.Disabled = type is null;
        _height.Editable = type is not null;
        _edge.Editable = type is not null;
        _variation.Editable = type is not null;
        _featureSize.Editable = type is not null;

        // Roughness carries the variation on, so a level type has none to set.
        bool roughens = type is { VariationMeters: > 0 };
        _roughness.Editable = roughens;
        _roughnessRow.TooltipText = type is null || roughens
            ? RoughnessTip
            : RoughnessNeedsVariation;
        _roughnessRow.Modulate = new Color(1, 1, 1, roughens ? 1 : 0.5f);
        if (type is null)
        {
            return;
        }

        _syncing = true;
        if (force || !_name.HasFocus())
        {
            _name.Text = type.Name;
        }

        _color.Color = type.Color.ToGodot();
        _climate.Select(_climate.GetItemIndex((int)type.Climate));
        _ground.Select(_ground.GetItemIndex((int)type.Ground));
        _plants.Select(_plants.GetItemIndex((int)type.Plants));
        _height.ShowMetric(type.HeightMeters);
        if (!_edgeDragging)
        {
            _edge.SetValueNoSignal(type.Edge);
        }

        _edgeName.Text = EdgeName(_edge.Value);
        _variation.ShowMetric(type.VariationMeters);
        _featureSize.ShowMetric(type.FeatureSizeKm);
        if (!_roughnessDragging)
        {
            _roughness.SetValueNoSignal(type.Roughness);
        }

        _roughnessName.Text = RoughnessName(_roughness.Value);
        _syncing = false;
    }

    // The brush's radius in km or miles (by the setting) on the selected body, with limits to
    // suit its size.
    private void ShowSize()
    {
        double perDegree = UnitText.Shown(Quantity.Distance, KmPerDegree());
        _syncing = true;
        _sizeField.Suffix = UnitText.Symbol(Quantity.Distance);
        _sizeField.MinValue = Math.Max(Math.Round(MinRadiusDegrees * perDegree), 1);
        _sizeField.MaxValue = Math.Round(MaxRadiusDegrees * perDegree);
        _sizeField.ShowValue(Math.Round(_sizeSlider.Value * perDegree));
        _syncing = false;
    }

    private void SizeChanged(bool fromSlider)
    {
        if (_syncing)
        {
            return;
        }

        if (!fromSlider)
        {
            _syncing = true;
            _sizeSlider.Value = Math.Clamp(
                _sizeField.Value / UnitText.Shown(Quantity.Distance, KmPerDegree()),
                MinRadiusDegrees, MaxRadiusDegrees);
            _syncing = false;
        }

        ShowSize();
        UpdateBrush();
    }

    private double KmPerDegree()
    {
        return Session is null ? 111.2 : Session.SelectedBody.RadiusKm * Math.PI / 180;
    }

    private void UpdateBrush()
    {
        if (Brush is null)
        {
            return;
        }

        SculptTool tool = _sculptButtons.First(pair => pair.Value.ButtonPressed).Key;
        bool byHeight = tool is SculptTool.Raise or SculptTool.Lower;
        _heightRow.Visible = byHeight;
        _amountRow.Visible = !byHeight;
        _heightValue.Text = UnitText.Format(Quantity.Length, _heightSlider.Value);
        _amountValue.Text = $"{_amountSlider.Value:N0}%";

        bool sculpting = _sculptButton.ButtonPressed;
        Brush.Sculpt = sculpting ? tool : null;
        Brush.Strength = byHeight ? Math.Round(_heightSlider.Value) : _amountSlider.Value / 100;
        Brush.Code = _eraseButton.ButtonPressed ? (byte)0 : _selectedCode ?? 0;
        Brush.RadiusDegrees = _sizeSlider.Value;
        bool shaping = _shapesButton.ButtonPressed;
        Brush.IsActive = Visible && _toolSwitch.ButtonPressed && !shaping
            && !_waterButton.ButtonPressed
            && Session is { SelectedBodyHasSurface: true }
            && (sculpting || _eraseButton.ButtonPressed || _selectedCode is not null);
        if (Shapes is not null)
        {
            Shapes.IsActive = Visible && _toolSwitch.ButtonPressed && shaping
                && Session is { SelectedBodyHasSurface: true };
        }
    }

    private void UpdateVisibility()
    {
        Visible = _open && (Toolbar?.Visible ?? true);
        UpdateBrush();
    }

    // Shows the Water section in Water mode, with its river lit; out of it, stops any placing
    // or drawing it started, and no river is lit.
    private void UpdateWater()
    {
        if (_water is null)
        {
            return;
        }

        bool shown = _open && _waterButton.ButtonPressed
            && Session is { SelectedBodyHasSurface: true };
        if (_water.Visible && !shown)
        {
            _water.StopPlacing();
            if (Rivers is not null)
            {
                Rivers.HighlightedId = null;
            }
        }

        _water.Visible = shown;
    }

    // A small square of a color, for the type list.
    private ImageTexture Swatch(RgbColor color)
    {
        if (!_swatches.TryGetValue(color, out ImageTexture? swatch))
        {
            Image image = Image.CreateEmpty(14, 14, false, Image.Format.Rgba8);
            image.Fill(color.ToGodot());
            swatch = ImageTexture.CreateFromImage(image);
            _swatches[color] = swatch;
        }

        return swatch;
    }

    // Water mode has its own Place and Draw buttons, so the switch is off there.
    private void ShowToolSwitch()
    {
        _toolSwitch.SetUnavailable(_waterButton.ButtonPressed
            ? "Water has its own buttons for placing lakes and drawing rivers"
            : null);
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
