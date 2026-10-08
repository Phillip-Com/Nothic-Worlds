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
/// The Terrain panel's Water mode (VISION.md BOD-11; owner's choices: rivers as lines, drawn or
/// natural, and lakes filled from a click to their own height): buttons to add a lake, a
/// natural river (from a clicked source, running downhill), or a drawn river (clicked points),
/// the selected body's rivers and lakes, and the selected one's name, width or surface,
/// whether a lake flows out, and what its water does, with Delete.
/// </summary>
public partial class WaterSection : VBoxContainer
{
    private ItemList _list = null!;
    private Control _editor = null!;
    private LineEdit _name = null!;
    private Control _levelRow = null!;
    private SpinBox _level = null!;
    private CheckBox _flowsOut = null!;
    private Control _widthRow = null!;
    private SpinBox _width = null!;
    private Label _status = null!;
    private Button _delete = null!;
    private Guid? _selectedId;
    private string _listSignature = "";
    private bool _showing;

    /// <summary>The open world. Set it before adding the section to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>Takes the click that places a lake or a natural river's source.</summary>
    public PinPlacer? Placer { get; init; }

    /// <summary>Draws a drawn river's points.</summary>
    public RegionEditor? LineDrawer { get; init; }

    /// <summary>Draws the rivers, lighter for the selected one.</summary>
    public RiverRenderer? Rivers { get; init; }

    /// <summary>The message line, for edits that can't be used.</summary>
    public MapToolbar? Toolbar { get; init; }

    /// <summary>Raised when the hint for the mode should change (something was selected).</summary>
    public event Action? HintChanged;

    public override void _Ready()
    {
        var adding = new HBoxContainer();
        adding.AddChild(CreateButton("New Lake", AddLake,
            "Click a low spot: the water fills the ground around it up to a surface you set"));
        adding.AddChild(CreateButton("Natural River", AddNaturalRiver,
            "Click its source: it runs downhill by the easiest way until it reaches water"));
        adding.AddChild(CreateButton("Drawn River", AddDrawnRiver,
            "Click its course from source to mouth; it keeps exactly that course"));
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
        Session.WaterChanged += Refresh;
        Refresh();
    }

    /// <summary>How to use the water tools, for the hint bar.</summary>
    public string Hint() => _selectedId is null
        ? "Add a lake or a river with the buttons above. Natural rivers follow the ground " +
            "and re-trace themselves when it changes."
        : "Change the selected river or lake below, or add another. Rivers are drawn their " +
            "true width, with a line along them so they show from afar.";

    /// <summary>Stops placing or drawing, when the section hides.</summary>
    public void StopPlacing()
    {
        Placer?.Cancel();
        if (LineDrawer?.IsDrawing == true)
        {
            LineDrawer.Stop();
        }
    }

    private Control BuildEditor()
    {
        _editor = new VBoxContainer();
        _name = new LineEdit
        {
            PlaceholderText = "Name",
            MaxLength = Math.Min(River.MaxNameLength, Lake.MaxNameLength),
        };
        _name.TextChanged += _ => Commit();
        _editor.AddChild(_name);

        _levelRow = new HBoxContainer();
        _levelRow.AddChild(new Label { Text = "Surface" });
        _level = new SpinBox
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "How high the lake's surface is (the water fills everything lower " +
                "joined to its spot)",
        }.WithUnit(Quantity.Length, Lake.MinLevelMeters, Lake.MaxLevelMeters, 10, 50);
        _level.WithLiveTyping(Refresh).ValueChanged += _ => Commit();
        _levelRow.AddChild(_level);
        _flowsOut = new CheckBox
        {
            Text = "Flows out",
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "A river leaves the lake from the lowest point of its shore",
        };
        _flowsOut.Toggled += _ => Commit();
        _editor.AddChild(_levelRow);
        _editor.AddChild(_flowsOut);

        _widthRow = new HBoxContainer();
        _widthRow.AddChild(new Label { Text = "Width" });
        _width = new SpinBox
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "How wide the river is at its mouth; it starts a fifth as wide",
        }.WithUnit(Quantity.Distance, River.MinWidthKm, River.MaxWidthKm, 0.01);
        _width.WithLiveTyping(Refresh).ValueChanged += _ => Commit();
        _widthRow.AddChild(_width);
        _editor.AddChild(_widthRow);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _editor.AddChild(_status);
        _delete = CreateButton("Delete", Delete, "Delete it (Ctrl+Z brings it back)");
        _editor.AddChild(_delete);
        return _editor;
    }

    private Body Body => Session.SelectedBody;

    private River? SelectedRiver => Session.World.Rivers.Find(r => r.Id == _selectedId);

    private Lake? SelectedLake => Session.World.Lakes.Find(l => l.Id == _selectedId);

    private void AddLake()
    {
        Placer?.Start(Body.Id, spot =>
        {
            (Lake? lake, string? problem) = Session.AddLake(Body.Id, spot);
            Added(lake?.Id, lake is null ? null : $"Added {lake.Name}: change its surface below.",
                problem);
        }, prompt: $"Click a low spot on {Body.Name} for the lake (Esc cancels).");
    }

    private void AddNaturalRiver()
    {
        Placer?.Start(Body.Id, spot =>
        {
            (River? river, string? problem) = Session.AddRiver(Body.Id, RiverKind.Natural,
                [spot]);
            Added(river?.Id, river is null ? null : $"Added {river.Name}: it runs downhill.",
                problem);
        }, prompt: $"Click the river's source on {Body.Name} (Esc cancels).");
    }

    private void AddDrawnRiver()
    {
        LineDrawer?.StartDrawingLine($"Click the river's course on {Body.Name}, from source " +
            "to mouth. Enter finishes; Backspace undoes a point; Esc cancels.", points =>
            {
                (River? river, string? problem) = Session.AddRiver(Body.Id, RiverKind.Drawn,
                    points);
                if (river is not null)
                {
                    Added(river.Id, $"Added {river.Name}.", null);
                }

                return problem;
            });
    }

    private void Added(Guid? id, string? message, string? problem)
    {
        if (id is null)
        {
            Toolbar?.ShowWarning($"Couldn't add it: {problem}.");
            return;
        }

        Toolbar?.ShowInfo(message ?? "");
        Select(id.Value);
    }

    private void Select(Guid id)
    {
        _selectedId = id;
        if (Rivers is not null)
        {
            Rivers.HighlightedId = id;
        }

        Refresh();
        HintChanged?.Invoke();
    }

    private void Delete()
    {
        if (SelectedRiver is River river)
        {
            Session.DeleteRiver(river.Id);
            Toolbar?.ShowInfo($"Deleted {river.Name} (Ctrl+Z to undo).");
        }
        else if (SelectedLake is Lake lake)
        {
            Session.DeleteLake(lake.Id);
            Toolbar?.ShowInfo($"Deleted {lake.Name} (Ctrl+Z to undo).");
        }
    }

    private void Commit()
    {
        if (_showing)
        {
            return;
        }

        string? problem = null;
        if (SelectedRiver is River river)
        {
            problem = Session.UpdateRiver(river with
            {
                Name = _name.Text,
                WidthKm = Math.Round(_width.MetricValue(), 2),
            });
        }
        else if (SelectedLake is Lake lake)
        {
            problem = Session.UpdateLake(lake with
            {
                Name = _name.Text,
                LevelMeters = (int)Math.Round(_level.MetricValue()),
                FlowsOut = _flowsOut.ButtonPressed,
            });
        }

        if (problem is not null)
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

        if (SelectedRiver?.BodyId != Body.Id && SelectedLake?.BodyId != Body.Id)
        {
            _selectedId = null;
        }

        ShowList();
        ShowSelected();
    }

    private void ShowList()
    {
        List<(Guid Id, string Text)> items =
        [
            .. Session.World.Lakes.Where(l => l.BodyId == Body.Id)
                .Select(l => (l.Id, $"{l.Name} (lake)")),
            .. Session.World.Rivers.Where(r => r.BodyId == Body.Id)
                .Select(r => (r.Id, r.Kind == RiverKind.Drawn
                    ? $"{r.Name} (drawn river)"
                    : $"{r.Name} (river)")),
        ];
        string signature = string.Join("|", items) + $"#{_selectedId}";
        if (signature == _listSignature)
        {
            return;
        }

        _listSignature = signature;
        _list.Clear();
        foreach ((Guid id, string text) in items)
        {
            int index = _list.AddItem(text);
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
        River? river = SelectedRiver;
        Lake? lake = SelectedLake;
        _editor.Visible = river is not null || lake is not null;
        if (!_editor.Visible)
        {
            return;
        }

        _showing = true;
        if (!_name.HasFocus())
        {
            _name.Text = river?.Name ?? lake!.Name;
        }

        _levelRow.Visible = lake is not null;
        _flowsOut.Visible = lake is not null;
        _widthRow.Visible = river is not null;
        if (lake is not null)
        {
            _level.ShowMetric(lake.LevelMeters);
            _flowsOut.SetPressedNoSignal(lake.FlowsOut);
        }
        else
        {
            _width.ShowMetric(river!.WidthKm);
        }

        _status.Text = Session.IsWorkingOutWater(Body.Id)
            ? "Working out where the water goes…"
            : lake is not null ? LakeStatus(lake) : RiverStatus(river!);
        _showing = false;
    }

    // What a lake's water does: how much ground it covers, or why it has none.
    private string LakeStatus(Lake lake)
    {
        if (Session.WaterOn(Body.Id)?.Lakes.GetValueOrDefault(lake.Id) is not LakeShape shape)
        {
            return "";
        }

        if (shape.Problem is string problem)
        {
            return $"No water yet: {problem}.";
        }

        double areaKm2 = shape.Cells.Count * CellAreaKm2();
        string area = UnitText.Format(Quantity.Area, areaKm2);
        string outflow = !lake.FlowsOut ? ""
            : RiverFrom(lake.Id) is { ReachesWater: false }
                ? " Its outflow can't find its way to water."
                : shape.Outflow is null ? " It has no shore to flow out over." : "";
        return $"Covers about {area}.{outflow}";
    }

    // What a river does: how far it runs, and whether it reaches water.
    private string RiverStatus(River river)
    {
        if (RiverFrom(river.Id) is not RiverCourseShown course)
        {
            return "";
        }

        double km = Length(course.Points) * Body.RadiusKm;
        string length = UnitText.Format(Quantity.Distance, km);
        return river.Kind == RiverKind.Drawn ? $"Runs {length}, as drawn."
            : course.ReachesWater ? $"Runs {length} downhill to water."
            : $"Runs {length}, then can't find its way to water (it ends where it stopped): " +
                "move its source, or add a lake or sea for it.";
    }

    private RiverCourseShown? RiverFrom(Guid id) =>
        Session.WaterOn(Body.Id)?.Rivers.FirstOrDefault(c => (c.RiverId ?? c.LakeId) == id);

    // The area of one water cell on the selected body (all are about equal).
    private double CellAreaKm2() =>
        4 * Math.PI * Body.RadiusKm * Body.RadiusKm / WaterCells.Count;

    private static double Length(IReadOnlyList<Vector3D> points)
    {
        double radians = 0;
        for (int i = 1; i < points.Count; i++)
        {
            radians += Math.Acos(Math.Clamp(points[i - 1].Dot(points[i]), -1, 1));
        }

        return radians;
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
