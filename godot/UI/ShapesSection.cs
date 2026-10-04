using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Model;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The Terrain panel's Shapes mode (VISION.md BOD-04): which kind of shape a click on the
/// planet places and whether it adds or cuts, the body's shapes, and the selected one's exact
/// values (its depth, sizes, and turn, which the handles on the globe don't cover), with Delete.
/// </summary>
public partial class ShapesSection : VBoxContainer
{
    private static readonly (ShapeKind Kind, string Name)[] _kinds =
    [
        (ShapeKind.Sphere, "Sphere"),
        (ShapeKind.Box, "Box"),
        (ShapeKind.Cylinder, "Cylinder"),
        (ShapeKind.Cone, "Cone"),
    ];

    private Dropdown _placeKind = null!;
    private Dropdown _placeOperation = null!;
    private ItemList _list = null!;
    private Control _editor = null!;
    private Dropdown _kind = null!;
    private Dropdown _operation = null!;
    private SpinBox _depth = null!;
    private SpinBox _width = null!;
    private SpinBox _height = null!;
    private SpinBox _length = null!;
    private SpinBox _turn = null!;
    private Label _heightLabel = null!;
    private Label _lengthLabel = null!;
    private bool _showing;

    /// <summary>The open world. Set it before adding the section to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>The handles on the globe, which hold the selection and place shapes.</summary>
    public ShapeHandles Handles { get; init; } = null!;

    /// <summary>The message line, for edits that can't be used.</summary>
    public MapToolbar? Toolbar { get; init; }

    public override void _Ready()
    {
        var placeRow = new HBoxContainer();
        placeRow.AddChild(new Label { Text = "New" });
        _placeKind = KindDropdown(() => Handles.PlaceKind = (ShapeKind)_placeKind.GetSelectedId());
        _placeKind.Select(_placeKind.GetItemIndex((int)Handles.PlaceKind));
        placeRow.AddChild(_placeKind);
        _placeOperation = OperationDropdown(() =>
            Handles.PlaceOperation = (ShapeOperation)_placeOperation.GetSelectedId());
        placeRow.AddChild(_placeOperation);
        AddChild(placeRow);
        AddChild(new Label
        {
            Text = "Click the planet to place one. Drag its middle to move it, its square to " +
                "resize it, and its round handle to turn it; the world is carved when you let go.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 1, 1, 0.6f),
        });

        _list = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 110),
            FocusMode = FocusModeEnum.None,
        };
        _list.ItemSelected += index =>
            Handles.SelectedShapeId = Guid.Parse(_list.GetItemMetadata((int)index).AsString());
        AddChild(_list);
        _editor = BuildEditor();
        AddChild(_editor);

        Session.Changed += Refresh;
        Session.SelectionChanged += Refresh;
        Handles.SelectionChanged += Refresh;
        Refresh();
    }

    private Control BuildEditor()
    {
        var box = new VBoxContainer();
        var grid = new GridContainer { Columns = 2 };
        grid.AddChild(new Label { Text = "Shape" });
        var row = new HBoxContainer();
        _kind = KindDropdown(Commit);
        row.AddChild(_kind);
        _operation = OperationDropdown(Commit);
        row.AddChild(_operation);
        grid.AddChild(row);
        _depth = Field(grid, "Depth", "km", -1e6, 1e6, out _,
            "How far its middle is above the body's radius (negative: below; minus the radius " +
            "is the body's center)");
        _width = Field(grid, "Width", "km", 1, 1e6, out _, "Across (a sphere's diameter)");
        _height = Field(grid, "Height", "km", 1, 1e6, out _heightLabel, "Up from the surface");
        _length = Field(grid, "Length", "km", 1, 1e6, out _lengthLabel, "Along its turn");
        _turn = Field(grid, "Turn", "°", 0, 360, out _, "Clockwise from north");
        _turn.WithWrapAround();
        box.AddChild(grid);
        var delete = new Button
        {
            Text = "Delete Shape",
            FocusMode = FocusModeEnum.None,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            TooltipText = "Remove the selected shape (Ctrl+Z brings it back)",
        };
        delete.Pressed += () =>
        {
            if (Handles.SelectedShapeId is Guid id)
            {
                Handles.SelectedShapeId = null;
                Session.DeleteShape(id);
            }
        };
        box.AddChild(delete);
        return box;
    }

    /// <summary>Shows the selected body's shapes and the selected one's values.</summary>
    public void Refresh()
    {
        if (!IsNodeReady())
        {
            return;
        }

        List<ShapeEdit> shapes = Session.SelectedBody.Surface.Shapes;
        _list.Clear();
        for (int i = 0; i < shapes.Count; i++)
        {
            int index = _list.AddItem($"{i + 1}. {shapes[i].Operation} {KindName(shapes[i].Kind)}");
            _list.SetItemMetadata(index, shapes[i].Id.ToString());
            if (shapes[i].Id == Handles.SelectedShapeId)
            {
                _list.Select(index);
            }
        }

        ShapeEdit? selected = shapes.FirstOrDefault(s => s.Id == Handles.SelectedShapeId);
        _editor.Visible = selected is not null;
        if (selected is null)
        {
            return;
        }

        _showing = true;
        _kind.Select(_kind.GetItemIndex((int)selected.Kind));
        _operation.Select(_operation.GetItemIndex((int)selected.Operation));
        _depth.ShowValue(selected.DepthKm);
        _width.ShowValue(selected.WidthKm);
        _height.ShowValue(selected.HeightKm);
        _length.ShowValue(selected.LengthKm);
        _turn.ShowValue(selected.TurnDegrees);
        bool hasHeight = selected.Kind != ShapeKind.Sphere;
        bool hasLength = selected.Kind == ShapeKind.Box;
        _height.Visible = _heightLabel.Visible = hasHeight;
        _length.Visible = _lengthLabel.Visible = hasLength;
        _showing = false;
    }

    private void Commit()
    {
        if (_showing || Session.SelectedBody.Surface.Shapes
            .FirstOrDefault(s => s.Id == Handles.SelectedShapeId) is not ShapeEdit shape)
        {
            return;
        }

        string? problem = Session.UpdateShape(shape with
        {
            Kind = (ShapeKind)_kind.GetSelectedId(),
            Operation = (ShapeOperation)_operation.GetSelectedId(),
            DepthKm = _depth.Value,
            WidthKm = _width.Value,
            HeightKm = _height.Value,
            LengthKm = _length.Value,
            TurnDegrees = _turn.Value % 360,
        });
        if (problem is not null)
        {
            Toolbar?.ShowError($"Can't use that: {problem}.");
            Refresh();
        }
    }

    private static Dropdown KindDropdown(Action changed)
    {
        var dropdown = new Dropdown { FocusMode = FocusModeEnum.None };
        foreach ((ShapeKind kind, string name) in _kinds)
        {
            dropdown.AddItem(name, (int)kind);
        }

        dropdown.ItemSelected += _ => changed();
        return dropdown;
    }

    private static Dropdown OperationDropdown(Action changed)
    {
        var dropdown = new Dropdown { FocusMode = FocusModeEnum.None };
        dropdown.AddItem("Add", (int)ShapeOperation.Add);
        dropdown.AddItem("Cut", (int)ShapeOperation.Cut);
        dropdown.ItemSelected += _ => changed();
        return dropdown;
    }

    private SpinBox Field(GridContainer grid, string name, string unit, double min, double max,
        out Label label, string tip)
    {
        label = new Label { Text = name };
        grid.AddChild(label);
        var field = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = 1,
            Suffix = unit,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = tip,
        };
        field.WithLiveTyping(Refresh).ValueChanged += _ => Commit();
        grid.AddChild(field);
        return field;
    }

    private static string KindName(ShapeKind kind) => _kinds.First(k => k.Kind == kind).Name;
}
