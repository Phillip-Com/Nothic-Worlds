using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The editor for one relationship between two journal entries (VISION.md LORE-04): who it
/// runs from and to, its kind, its own words (needed for Other), and when it began and ended,
/// if ever (dates in the selected body's calendar, like events'). Opened by linking two boxes
/// on a diagram, clicking a line, or from the Journal panel. Save applies it as one undo step.
/// </summary>
public partial class RelationshipDialog : ConfirmationDialog
{
    private const string DeleteAction = "delete";

    private OptionButton _from = null!;
    private OptionButton _kind = null!;
    private OptionButton _to = null!;
    private LineEdit _label = null!;
    private CheckBox _started = null!;
    private DateFields _start = null!;
    private CheckBox _ended = null!;
    private DateFields _end = null!;
    private Label _problem = null!;
    private Button _delete = null!;
    private Relationship? _editing;
    private bool _isNew;

    /// <summary>The open world. Set it before adding the dialog to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    public override void _Ready()
    {
        OkButtonText = "Save";
        DialogHideOnOk = false;
        _delete = AddButton("Delete Relationship", right: false, action: DeleteAction);
        _delete.TooltipText = "Delete this relationship (Ctrl+Z brings it back); the entries stay";
        Confirmed += Save;
        CustomAction += action =>
        {
            if (action == DeleteAction && _editing is Relationship relationship && !_isNew)
            {
                Session.DeleteRelationship(relationship.Id);
                Hide();
            }
        };

        var layout = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        AddChild(layout);
        var grid = new GridContainer { Columns = 2 };
        _from = Field(grid, "From");
        _kind = Field(grid, "Is");
        foreach (RelationshipKind kind in LoreWords.RelationshipKinds)
        {
            _kind.AddItem(LoreWords.Kind(kind));
        }

        _kind.ItemSelected += _ => ShowLabelHint();
        _to = Field(grid, "To");
        _label = new LineEdit
        {
            MaxLength = Relationship.MaxLabelLength,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        grid.AddChild(new Label { Text = "In words" });
        grid.AddChild(_label);
        layout.AddChild(grid);

        _started = new CheckBox
        {
            Text = "Began on…",
            TooltipText = "Leave off for a tie that has always been",
        };
        _started.Toggled += on => _start.Visible = on;
        layout.AddChild(_started);
        _start = new DateFields { Visible = false };
        layout.AddChild(_start);
        _ended = new CheckBox
        {
            Text = "Ended on…",
            TooltipText = "Leave off for a tie that still holds",
        };
        _ended.Toggled += on => _end.Visible = on;
        layout.AddChild(_ended);
        _end = new DateFields { Visible = false };
        layout.AddChild(_end);

        _problem = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1.0f, 0.55f, 0.5f),
        };
        layout.AddChild(_problem);
    }

    /// <summary>Opens the editor on a new relationship between two entries.</summary>
    public void New(Guid fromEntryId, Guid toEntryId)
    {
        _isNew = true;
        Open(new Relationship
        {
            FromEntryId = fromEntryId,
            ToEntryId = toEntryId,
            Kind = RelationshipKind.AllyOf,
        });
    }

    /// <summary>Opens the editor on an existing relationship.</summary>
    public void Edit(Relationship relationship)
    {
        _isNew = false;
        Open(relationship);
    }

    private void Open(Relationship relationship)
    {
        _editing = relationship;
        Body body = Session.SelectedBody;
        Title = $"{(_isNew ? "New relationship" : "Relationship")} (dates on {body.Name})";
        _delete.Visible = !_isNew;
        _problem.Text = "";
        ShowEntries(_from, relationship.FromEntryId);
        ShowEntries(_to, relationship.ToEntryId);
        _kind.Select(Array.IndexOf(LoreWords.RelationshipKinds, relationship.Kind));
        _label.Text = relationship.Label;
        ShowLabelHint();
        double now = Session.World.TimeDays;
        _started.ButtonPressed = relationship.StartDays is not null;
        _start.ShowTime(body, relationship.StartDays ?? now);
        _start.Visible = _started.ButtonPressed;
        _ended.ButtonPressed = relationship.EndDays is not null;
        _end.ShowTime(body, relationship.EndDays ?? now);
        _end.Visible = _ended.ButtonPressed;
        PopupCentered();
    }

    private void Save()
    {
        if (_editing is not Relationship original)
        {
            return;
        }

        var changed = original with
        {
            FromEntryId = Chosen(_from),
            ToEntryId = Chosen(_to),
            Kind = LoreWords.RelationshipKinds[_kind.Selected],
            Label = _label.Text.Trim(),
            StartDays = _started.ButtonPressed ? _start.TimeDays : null,
            EndDays = _ended.ButtonPressed ? _end.TimeDays : null,
        };
        string? problem = _isNew
            ? Session.AddRelationship(changed)
            : Session.UpdateRelationship(changed);
        if (problem is not null)
        {
            _problem.Text = $"Can't save: {problem}.";
            return;
        }

        Hide();
    }

    // Other needs its own words; for the rest they're optional extra detail.
    private void ShowLabelHint()
    {
        bool other = LoreWords.RelationshipKinds[_kind.Selected] == RelationshipKind.Other;
        _label.PlaceholderText = other
            ? "Needed: how they're tied (\"owes a debt to\")"
            : "Optional (\"eldest son of\")";
    }

    private void ShowEntries(OptionButton list, Guid selected)
    {
        list.Clear();
        foreach (JournalEntry entry in Session.World.Journal
            .OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase))
        {
            list.AddItem(entry.Title);
            list.SetItemMetadata(list.ItemCount - 1, entry.Id.ToString());
            if (entry.Id == selected)
            {
                list.Select(list.ItemCount - 1);
            }
        }
    }

    private static Guid Chosen(OptionButton list) =>
        list.Selected < 0 ? Guid.Empty : Guid.Parse(list.GetItemMetadata(list.Selected).AsString());

    private static OptionButton Field(GridContainer grid, string text)
    {
        var field = new Dropdown { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddChild(new Label { Text = text });
        grid.AddChild(field);
        return field;
    }
}
