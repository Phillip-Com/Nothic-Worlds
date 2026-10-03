using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Model;
using NothicWorlds.Interop;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The Terrain panel (VISION.md BOD-05; owner's choice: its own panel, one at a time with the
/// others on the right): Paint or Erase, the brush size, and the world's terrain types, which
/// can be added, renamed, recolored, and deleted. While it's open, dragging on the selected
/// planet or moon paints with the selected type (<see cref="TerrainBrush"/>).
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

    private Label _heading = null!;
    private Label _note = null!;
    private Control _tools = null!;
    private Button _paintButton = null!;
    private Button _eraseButton = null!;
    private HSlider _sizeSlider = null!;
    private SpinBox _sizeField = null!;
    private ItemList _list = null!;
    private Button _deleteButton = null!;
    private LineEdit _name = null!;
    private ColorPickerButton _color = null!;
    private OptionButton _climate = null!;
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

        _heading = new Label();
        layout.AddChild(_heading);
        _note = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        layout.AddChild(_note);
        _tools = BuildTools();
        layout.AddChild(_tools);
        layout.AddChild(new HSeparator());
        layout.AddChild(new Label { Text = "Terrain Types" });
        layout.AddChild(BuildTypeList());
        layout.AddChild(BuildTypeEditor());
        layout.AddChild(new HSeparator());
        layout.AddChild(new Label
        {
            Text = "Drag on the planet to paint. To turn the view, drag off the planet or use " +
                "the arrow keys. Each stroke is one undo step (Ctrl+Z).",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 1, 1, 0.6f),
        });

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
        Refresh();
        UpdateVisibility();
    }

    // Paint or Erase, and the brush size.
    private Control BuildTools()
    {
        var tools = new VBoxContainer();
        var modes = new HBoxContainer();
        var group = new ButtonGroup();
        _paintButton = CreateButton("Paint", UpdateBrush, "Paint with the selected type");
        _eraseButton = CreateButton("Erase", UpdateBrush, "Remove painted terrain");
        foreach (Button button in new[] { _paintButton, _eraseButton })
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
            Suffix = "km",
            Step = 1,
            CustomMinimumSize = new Vector2(100, 0),
            TooltipText = "The brush's radius on the selected body",
        };
        _sizeField.WithLiveTyping(ShowSize).ValueChanged += _ => SizeChanged(fromSlider: false);
        sizeRow.AddChild(_sizeField);
        tools.AddChild(sizeRow);
        return tools;
    }

    private Control BuildTypeList()
    {
        var box = new VBoxContainer();
        _list = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 200),
            FocusMode = Control.FocusModeEnum.None,
        };
        _list.ItemSelected += index =>
        {
            _selectedCode = (byte)_list.GetItemMetadata((int)index).AsInt32();
            _paintButton.ButtonPressed = true;
            _problem.Text = "";
            Refresh();
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
        _note.Text = canPaint ? "" : "Stars and comets can't be painted. Select a planet or moon.";
        _note.Visible = !canPaint;
        _tools.Visible = canPaint;
        ShowList();
        ShowSelected(force: false);
        ShowSize();
        UpdateBrush();
    }

    private void ShowList()
    {
        IReadOnlyList<TerrainType> types = Session!.TerrainTypes;
        string signature = string.Join("|", types.Select(t => $"{t.Code}:{t.Name}:{t.Color}"))
            + $"#{_selectedCode}";
        if (signature == _listSignature)
        {
            return;
        }

        _listSignature = signature;
        _list.Clear();
        foreach (TerrainType type in types)
        {
            int index = _list.AddItem(type.Name, Swatch(type.Color));
            _list.SetItemMetadata(index, (int)type.Code);
            if (type.Code == _selectedCode)
            {
                _list.Select(index);
                _list.EnsureCurrentIsVisible();
            }
        }
    }

    // Fills the editor from the selected type, leaving the name alone while it's being typed.
    private void ShowSelected(bool force)
    {
        TerrainType? type = Selected;
        _deleteButton.Disabled = type is null;
        _name.Editable = type is not null;
        _color.Disabled = type is null;
        _climate.Disabled = type is null;
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
        _syncing = false;
    }

    // The brush's radius in km on the selected body, with limits to suit its size.
    private void ShowSize()
    {
        double kmPerDegree = KmPerDegree();
        _syncing = true;
        _sizeField.MinValue = Math.Max(Math.Round(MinRadiusDegrees * kmPerDegree), 1);
        _sizeField.MaxValue = Math.Round(MaxRadiusDegrees * kmPerDegree);
        _sizeField.ShowValue(Math.Round(_sizeSlider.Value * kmPerDegree));
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
                _sizeField.Value / KmPerDegree(), MinRadiusDegrees, MaxRadiusDegrees);
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

        Brush.Code = _eraseButton.ButtonPressed ? (byte)0 : _selectedCode ?? 0;
        Brush.RadiusDegrees = _sizeSlider.Value;
        Brush.IsActive = Visible && Session is { SelectedBodyHasSurface: true }
            && (_eraseButton.ButtonPressed || _selectedCode is not null);
    }

    private void UpdateVisibility()
    {
        Visible = _open && (Toolbar?.Visible ?? true);
        UpdateBrush();
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
