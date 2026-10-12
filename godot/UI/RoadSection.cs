using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Measurement;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The Terrain panel's Roads mode (VISION.md BOD-13; owner's choices: roads as lines, drawn or
/// routed, in four styles): a style for new roads, buttons to add a routed road (it finds the
/// easiest way through clicked places) or a drawn one (it keeps the clicked line), joining
/// other roads where a click lands near one, the selected body's roads, and the selected one's
/// name, style, and width, with what its way does and Delete.
/// </summary>
public partial class RoadSection : VBoxContainer
{
    // How far apart the spots along a road are that a click can join it at, in degrees.
    private const double JoinStepDegrees = 0.02;

    private static readonly RoadStyle[] _styles = Enum.GetValues<RoadStyle>();

    // A small square of each style's color, for the list.
    private static readonly Dictionary<RoadStyle, ImageTexture> _swatches = [];

    private OptionButton _newStyle = null!;
    private ItemList _list = null!;
    private Control _editor = null!;
    private LineEdit _name = null!;
    private OptionButton _style = null!;
    private SpinBox _width = null!;
    private Label _widthLabel = null!;
    private Label _status = null!;
    private Guid? _selectedId;
    private string _listSignature = "";
    private bool _showing;

    /// <summary>The open world. Set it before adding the section to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>Draws a road's points.</summary>
    public RegionEditor? LineDrawer { get; init; }

    /// <summary>Draws the roads, lighter for the selected one.</summary>
    public RoadRenderer? Roads { get; init; }

    /// <summary>The message line, for edits that can't be used.</summary>
    public MapToolbar? Toolbar { get; init; }

    /// <summary>Raised when the hint for the mode should change (something was selected).</summary>
    public event Action? HintChanged;

    public override void _Ready()
    {
        var styleRow = new HBoxContainer();
        styleRow.AddChild(new Label { Text = "New" });
        _newStyle = StylePicker("The style of the next road you add");
        _newStyle.Select(0);
        styleRow.AddChild(_newStyle);
        AddChild(styleRow);

        var adding = new HBoxContainer();
        adding.AddChild(CreateButton("Routed Road", () => AddRoad(RoadKind.Routed),
            "Click the places it passes through: it finds the easiest way between them for " +
            "its style"));
        adding.AddChild(CreateButton("Drawn Road", () => AddRoad(RoadKind.Drawn),
            "Click its course; it keeps exactly that line"));
        AddChild(adding);

        _list = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 140),
            FocusMode = Control.FocusModeEnum.None,
        };
        _list.ItemSelected += index => Select(Guid.Parse(_list.GetItemMetadata((int)index)
            .AsString()));
        AddChild(_list);
        AddChild(BuildEditor());

        Session.Changed += Refresh;
        Session.SelectionChanged += Refresh;
        Session.RoadsChanged += Refresh;
        Refresh();
    }

    /// <summary>How to use the road tools, for the hint bar.</summary>
    public string Hint() => _selectedId is null
        ? "Pick a style, then add a road: routed ones find the easiest way through the places " +
            "you click, drawn ones keep your line. Click near another road to join it."
        : "Change the selected road below, or add another. Routed roads find their way again " +
            "when the ground or the water changes.";

    /// <summary>Stops drawing, when the section hides.</summary>
    public void StopPlacing()
    {
        if (LineDrawer?.IsDrawing == true)
        {
            LineDrawer.Stop();
        }
    }

    private Control BuildEditor()
    {
        _editor = new VBoxContainer();
        _name = new LineEdit { PlaceholderText = "Name", MaxLength = Road.MaxNameLength };
        _name.TextChanged += _ => Commit();
        _editor.AddChild(_name);

        var styleRow = new HBoxContainer();
        styleRow.AddChild(new Label { Text = "Style" });
        _style = StylePicker("What the road is like: how it finds its way and how it looks");
        _style.ItemSelected += _ => Commit();
        styleRow.AddChild(_style);
        _editor.AddChild(styleRow);

        var widthRow = new HBoxContainer();
        _widthLabel = new Label { Text = "Width" };
        widthRow.AddChild(_widthLabel);
        _width = new SpinBox { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }
            .WithUnit(Quantity.Length, Road.MinWidthMeters, Road.MaxWidthMeters, 1);
        _width.WithLiveTyping(Refresh).ValueChanged += _ => Commit();
        widthRow.AddChild(_width);
        _editor.AddChild(widthRow);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _editor.AddChild(_status);
        _editor.AddChild(CreateButton("Delete", Delete, "Delete it (Ctrl+Z brings it back)"));
        return _editor;
    }

    private Body Body => Session.SelectedBody;

    private Road? SelectedRoad => Session.World.Roads.Find(r => r.Id == _selectedId);

    private void AddRoad(RoadKind kind)
    {
        RoadStyle style = _styles[Math.Max(0, _newStyle.Selected)];
        string what = kind == RoadKind.Routed
            ? "the places it passes through"
            : "its course";
        LineDrawer?.StartDrawingLine($"Click {what} on {Body.Name}, first to last; a click near " +
            "another road joins it. Enter finishes; Backspace undoes a point; Esc cancels.",
            points =>
            {
                (Road? road, string? problem) = Session.AddRoad(Body.Id, style, kind, points);
                if (road is not null)
                {
                    Toolbar?.ShowInfo(kind == RoadKind.Routed
                        ? $"Added {road.Name}: finding its way…"
                        : $"Added {road.Name}.");
                    Select(road.Id);
                }

                return problem;
            }, JoinSpots);
    }

    // The spots along the selected body's roads a click can join.
    private IEnumerable<GeoCoordinate> JoinSpots()
    {
        double step = double.DegreesToRadians(JoinStepDegrees);
        foreach (RoadCourseShown way in Session.RoadsOn(Body.Id))
        {
            for (int i = 0; i < way.Path.Count; i++)
            {
                yield return SphericalPolygon.FromUnit(way.Path[i]);
                if (i + 1 == way.Path.Count)
                {
                    continue;
                }

                // Long stretches (drawn roads') get spots between their points.
                Vector3D from = way.Path[i], to = way.Path[i + 1];
                int pieces = (int)Math.Ceiling((to - from).Length / step);
                for (int piece = 1; piece < pieces; piece++)
                {
                    Vector3D between = from + (to - from) * ((double)piece / pieces);
                    yield return SphericalPolygon.FromUnit(between * (1 / between.Length));
                }
            }
        }
    }

    private void Select(Guid id)
    {
        _selectedId = id;
        if (Roads is not null)
        {
            Roads.HighlightedId = id;
        }

        Refresh();
        HintChanged?.Invoke();
    }

    private void Delete()
    {
        if (SelectedRoad is Road road)
        {
            Session.DeleteRoad(road.Id);
            Toolbar?.ShowInfo($"Deleted {road.Name} (Ctrl+Z to undo).");
        }
    }

    private void Commit()
    {
        if (_showing || SelectedRoad is not Road road)
        {
            return;
        }

        if (Session.UpdateRoad(road with
        {
            Name = _name.Text,
            Style = _styles[Math.Max(0, _style.Selected)],
            WidthMeters = Math.Round(_width.MetricValue(), 1),
        }) is string problem)
        {
            Toolbar?.ShowWarning($"Not saved yet: {problem}.");
        }
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        if (SelectedRoad?.BodyId != Body.Id)
        {
            _selectedId = null;
        }

        ShowList();
        ShowSelected();
    }

    private void ShowList()
    {
        List<(Guid Id, string Text, RoadStyle Style)> items =
        [
            .. Session.World.Roads.Where(r => r.BodyId == Body.Id)
                .Select(r => (r.Id, r.Kind == RoadKind.Drawn
                    ? $"{r.Name} ({WorldSession.StyleName(r.Style).ToLowerInvariant()}, drawn)"
                    : $"{r.Name} ({WorldSession.StyleName(r.Style).ToLowerInvariant()})",
                    r.Style)),
        ];
        string signature = string.Join("|", items) + $"#{_selectedId}";
        if (signature == _listSignature)
        {
            return;
        }

        _listSignature = signature;
        _list.Clear();
        foreach ((Guid id, string text, RoadStyle style) in items)
        {
            int index = _list.AddItem(text, Swatch(style));
            _list.SetItemMetadata(index, id.ToString());
            if (id == _selectedId)
            {
                _list.Select(index);
                _list.EnsureCurrentIsVisible();
            }
        }
    }

    private void ShowSelected()
    {
        Road? road = SelectedRoad;
        _editor.Visible = road is not null;
        if (road is null)
        {
            return;
        }

        _showing = true;
        if (!_name.HasFocus())
        {
            _name.Text = road.Name;
        }

        _style.Select(Array.IndexOf(_styles, road.Style));
        _width.ShowMetric(road.WidthMeters);
        bool band = road.Style == RoadStyle.DirtPaths;
        _widthLabel.Text = band ? "Spread" : "Width";
        _width.TooltipText = band
            ? "How wide a band the paths split, wander, and rejoin within"
            : "How wide the road is";
        _status.Text = Session.IsWorkingOutRoads(Body.Id)
            ? "Finding the way…"
            : Status(road);
        _showing = false;
    }

    // What a road's way does: how far it runs, and anything it couldn't find.
    private string Status(Road road)
    {
        if (Session.RoadsOn(Body.Id).FirstOrDefault(w => w.RoadId == road.Id)
            is not RoadCourseShown way)
        {
            return "";
        }

        string length = UnitText.Format(Quantity.Distance, Length(way.Path) * Body.RadiusKm);
        return way.Problem is string problem ? $"Runs {length}, but {problem}."
            : road.Kind == RoadKind.Drawn ? $"Runs {length}, as drawn."
            : $"Runs {length} by the easiest way for {Travelers(road.Style)}.";
    }

    private static string Travelers(RoadStyle style) => style switch
    {
        RoadStyle.DirtPaths => "people on foot",
        RoadStyle.CartTrack => "carts",
        RoadStyle.OldRoad => "an old highway's builders",
        _ => "a highway's builders",
    };

    private static double Length(IReadOnlyList<Vector3D> points)
    {
        double radians = 0;
        for (int i = 1; i < points.Count; i++)
        {
            radians += Math.Acos(Math.Clamp(points[i - 1].Dot(points[i]), -1, 1));
        }

        return radians;
    }

    private static ImageTexture Swatch(RoadStyle style)
    {
        if (!_swatches.TryGetValue(style, out ImageTexture? swatch))
        {
            Image image = Image.CreateEmpty(14, 14, false, Image.Format.Rgba8);
            image.Fill(RoadRenderer.ColorOf(style));
            swatch = ImageTexture.CreateFromImage(image);
            _swatches[style] = swatch;
        }

        return swatch;
    }

    private static OptionButton StylePicker(string tooltip)
    {
        var picker = new OptionButton
        {
            TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        foreach (RoadStyle style in _styles)
        {
            picker.AddItem(WorldSession.StyleName(style));
        }

        return picker;
    }

    private static Button CreateButton(string text, Action pressed, string tooltip)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        button.Pressed += pressed;
        return button;
    }
}
