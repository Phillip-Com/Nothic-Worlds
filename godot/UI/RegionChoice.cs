using Godot;
using NothicWorlds.Core.Model;

namespace NothicWorlds.UI;

/// <summary>
/// A "Region" dropdown for a place (VISION.md LORE-01; owner's choice: regions are places):
/// anywhere on the place's body, or one of the regions on it. Shown only when that body has
/// regions. Used by the journal editor and the event editor.
/// </summary>
public partial class RegionChoice : HBoxContainer
{
    private OptionButton _choice = null!;
    private bool _showing;

    /// <summary>Raised when the user picks a different region (or none).</summary>
    public event Action? Changed;

    public override void _Ready()
    {
        AddChild(new Label { Text = "Region" });
        _choice = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
            TooltipText = "The region on the body it's in (regions are drawn in the Regions panel)",
        };
        _choice.ItemSelected += _ =>
        {
            if (!_showing)
            {
                Changed?.Invoke();
            }
        };
        AddChild(_choice);
    }

    /// <summary>Shows the regions on a place's body, with the place's region chosen.</summary>
    public void ShowFor(World world, LoreLocation? place)
    {
        _showing = true;
        _choice.Clear();
        List<Region> regions = place is null
            ? []
            : [.. world.Regions.Where(r => r.BodyId == place.BodyId)];
        Visible = regions.Count > 0;
        string bodyName = world.Bodies.FirstOrDefault(b => b.Id == place?.BodyId)?.Name ?? "";
        _choice.AddItem($"Anywhere on {bodyName}");
        _choice.SetItemMetadata(0, "");
        _choice.Select(0);
        foreach (Region region in regions)
        {
            _choice.AddItem(region.Name);
            _choice.SetItemMetadata(_choice.ItemCount - 1, region.Id.ToString());
            if (region.Id == place?.RegionId)
            {
                _choice.Select(_choice.ItemCount - 1);
            }
        }

        _showing = false;
    }

    /// <summary>
    /// The place with the chosen region (none if the place's body changed, so the choice no
    /// longer applies).
    /// </summary>
    public LoreLocation? Apply(World world, LoreLocation? place)
    {
        if (place is null)
        {
            return null;
        }

        Guid? region = _choice.Selected > 0
            && Guid.TryParse(_choice.GetItemMetadata(_choice.Selected).AsString(), out Guid id)
            && world.Regions.Any(r => r.Id == id && r.BodyId == place.BodyId)
                ? id
                : null;
        return place with { RegionId = region };
    }
}
