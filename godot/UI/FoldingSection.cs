using Godot;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// A part of a panel that folds away under its heading (VISION.md UI-07): click the heading to
/// open or close it. Whether it's open is remembered on this computer. Add what it holds to
/// <see cref="Content"/>.
/// </summary>
public partial class FoldingSection : VBoxContainer
{
    private readonly Button _heading;
    private readonly string _id;
    private readonly string _title;

    /// <summary>
    /// Makes a section titled <paramref name="title"/>, remembered as <paramref name="id"/>
    /// (unique across the app), open at first if <paramref name="openAtFirst"/>.
    /// </summary>
    public FoldingSection(string id, string title, bool openAtFirst)
    {
        _id = id;
        _title = title;
        Content = new VBoxContainer { Name = "Content" };
        _heading = new Button
        {
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        _heading.AddThemeFontSizeOverride("font_size", 16);
        _heading.AddThemeColorOverride("font_color", AppTheme.Accent.Lightened(0.45f));
        _heading.AddThemeColorOverride("font_hover_color", Colors.White);
        _heading.Pressed += () => SetOpen(!Content.Visible, remember: true);
        AddChild(_heading);
        AddChild(Content);
        SetOpen(AppSettings.IsSectionOpen(id, openAtFirst), remember: false);
    }

    /// <summary>What the section holds: shown when it's open.</summary>
    public VBoxContainer Content { get; }

    /// <summary>Whether the section is open.</summary>
    public bool IsOpen => Content.Visible;

    private void SetOpen(bool open, bool remember)
    {
        Content.Visible = open;
        _heading.Text = (open ? "▾  " : "▸  ") + _title;
        _heading.TooltipText = open ? $"Fold {_title} away" : $"Show {_title}";
        if (remember)
        {
            AppSettings.SetSectionOpen(_id, open);
        }
    }
}
