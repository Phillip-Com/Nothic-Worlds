using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// Keyboard behavior for number fields: while one is being edited, the Up and Down arrow keys
/// raise and lower its value by one step (ten with Shift), instead of moving the camera.
/// </summary>
public static class NumberFields
{
    /// <summary>Makes Up/Down change the field's value. Returns the field, for chaining.</summary>
    public static SpinBox WithArrowKeys(this SpinBox field)
    {
        LineEdit text = field.GetLineEdit();
        text.GuiInput += @event =>
        {
            if (@event is not InputEventKey { Pressed: true } key
                || key.Keycode is not (Key.Up or Key.Down))
            {
                return;
            }

            field.Apply();  // Use anything typed so far before stepping from it.
            double steps = key.ShiftPressed ? 10 : 1;
            field.Value += (key.Keycode == Key.Up ? 1 : -1) * field.Step * steps;
            text.AcceptEvent();
        };
        return field;
    }
}
