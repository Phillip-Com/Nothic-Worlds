using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// Keyboard and live-editing behavior for number fields:
/// <list type="bullet">
/// <item>While one is being edited, Up and Down raise and lower its value by one step (ten with
/// Shift), instead of moving the camera.</item>
/// <item>Live fields apply as the user types, so the world updates immediately (owner's
/// request: live updates while editing a body).</item>
/// </list>
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

    /// <summary>
    /// Makes the field apply its value as the user types (and Up/Down work). Pair it with
    /// <see cref="ShowValue"/>, so updates from the world don't overwrite what's being typed.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="editingFinished">
    /// Called when the user leaves the field, to show the value the world actually took (it
    /// may have been adjusted, e.g. an angle wrapped to 0–360).
    /// </param>
    public static SpinBox WithLiveTyping(this SpinBox field, Action editingFinished)
    {
        field.UpdateOnTextChanged = true;
        field.GetLineEdit().FocusExited += editingFinished;
        return field.WithArrowKeys();
    }

    /// <summary>
    /// Shows a value from the world, unless the user is typing in the field right now. Doesn't
    /// count as an edit.
    /// </summary>
    public static void ShowValue(this SpinBox field, double value)
    {
        if (!IsBeingTyped(field))
        {
            field.SetValueNoSignal(value);
        }
    }

    /// <summary>True while the user is typing in the field.</summary>
    public static bool IsBeingTyped(SpinBox field) => field.GetLineEdit().HasFocus();
}
