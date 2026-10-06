using Godot;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The screen the app opens to (VISION.md UI-06; owner's choice): New World, Open World,
/// the worlds opened or saved lately, Settings, and Quit. It covers the whole window, and the
/// 3D view stops drawing behind it. It goes away when a world is started or opened by any
/// means (including recovering unsaved changes, whose prompt shows on top of it).
/// </summary>
public partial class StartScreen : CanvasLayer
{
    private const float ColumnWidth = 420.0f;
    private const int TitleFontSize = 44;

    private static readonly Color _backgroundColor = new(0.035f, 0.04f, 0.065f);
    private static readonly Color _quietColor = new(1, 1, 1, 0.6f);

    private VBoxContainer _recent = null!;

    /// <summary>The open world, to know when another one replaces it.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>Opening worlds and the Settings window, as the File menu does them.</summary>
    [Export] public FileMenu? Files { get; set; }

    /// <summary>Whether the start screen is showing.</summary>
    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Layer = 20;  // Above the toolbar, panels, and editors.
        Visible = false;
        if (Session is null || Files is null)
        {
            GD.PushError("StartScreen needs a world session and the File menu.");
            return;
        }

        var background = new ColorRect { Color = _backgroundColor };
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        background.AddChild(center);
        center.AddChild(BuildColumn());

        // Opening, recovering, or starting a world replaces the one shown at launch.
        Session.WorldClosed += _ => Close();
    }

    /// <summary>Shows the start screen, with the recent worlds as they are now.</summary>
    public void Open()
    {
        ShowRecentWorlds();
        Visible = true;
        GetViewport().Disable3D = true;  // Nothing behind it needs drawing.
    }

    /// <summary>Hides the start screen, back to the world.</summary>
    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        GetViewport().Disable3D = false;
    }

    private VBoxContainer BuildColumn()
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(ColumnWidth, 0) };
        column.AddThemeConstantOverride("separation", 10);

        var title = new Label
        {
            Text = "Nothic Worlds",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", TitleFontSize);
        column.AddChild(title);
        column.AddChild(new Label
        {
            Text = "Design star systems and the worlds in them",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = _quietColor,
        });
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 16) });

        column.AddChild(CreateButton("New World",
            "Start with a sun and one planet, ready to shape", Close));
        column.AddChild(CreateButton("Open World…", "Open a world file (.nworld)", () =>
        {
            // The dialog shows over the screen; the screen closes once a world opens.
            Files!.ShowOpenDialog();
        }));
        column.AddChild(CreateButton("Settings…",
            "Graphics quality and units, for this computer", () => Files!.ShowSettings()));
        column.AddChild(CreateButton("Quit", "Close Nothic Worlds", Quit));

        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 16) });
        column.AddChild(new Label { Text = "Recent Worlds" });
        _recent = new VBoxContainer();
        column.AddChild(_recent);
        return column;
    }

    // The recent worlds as buttons; a file that's no longer there is shown but can't be opened.
    private void ShowRecentWorlds()
    {
        foreach (Node child in _recent.GetChildren())
        {
            child.QueueFree();
        }

        IReadOnlyList<string> paths = AppSettings.RecentWorldPaths;
        if (paths.Count == 0)
        {
            _recent.AddChild(new Label
            {
                Text = "None yet: worlds you open or save will be listed here.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = _quietColor,
            });
            return;
        }

        foreach (string path in paths)
        {
            var button = new Button
            {
                Text =
                    $"{Path.GetFileNameWithoutExtension(path)}   ({Path.GetDirectoryName(path)})",
                Alignment = HorizontalAlignment.Left,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                Flat = true,
                FocusMode = Control.FocusModeEnum.None,
            };
            DisabledTip.Apply(button, $"Open {path}", File.Exists(path)
                ? null
                : $"Not found: {path} (it may have been moved, renamed, or deleted)");
            button.Pressed += () => _ = OpenRecentAsync(path);
            _recent.AddChild(button);
        }
    }

    private async Task OpenRecentAsync(string path)
    {
        // A failure keeps the screen up, with the reason in the toolbar's message line under it,
        // so it closes first and the message can be read.
        Close();
        await Files!.OpenPathAsync(path);
    }

    private void Quit()
    {
        // The same as closing the window, which asks about unsaved changes first.
        GetTree().Root.PropagateNotification((int)Node.NotificationWMCloseRequest);
    }

    private static Button CreateButton(string text, string tooltip, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 40),
        };
        button.Pressed += pressed;
        return button;
    }
}
