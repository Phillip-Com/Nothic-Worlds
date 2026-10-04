using System.Globalization;
using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// Keyboard and live-editing behavior for number fields:
/// <list type="bullet">
/// <item>While one is being edited, Up and Down raise and lower its value by one step (ten with
/// Shift), instead of moving the camera.</item>
/// <item>Live fields apply as the user types, so the world updates immediately (owner's
/// request: live updates while editing a body).</item>
/// <item>Full-circle angles wrap around: past the top comes back at the bottom, and the other
/// way (owner's request). Angles with real limits, like a tilt of 0–180°, don't wrap.</item>
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
        // Godot's own live update (UpdateOnTextChanged) turns a lone "-" into 0, so a negative
        // number couldn't be typed. Apply only text that is already a whole number.
        LineEdit text = field.GetLineEdit();
        text.TextChanged += typed =>
        {
            if (!IsCompleteNumber(typed, field.Suffix))
            {
                return;
            }

            int caret = text.CaretColumn;
            field.Apply();
            text.CaretColumn = caret;  // Applying rewrites the text, which moves the caret.
        };
        text.FocusExited += editingFinished;
        return field.WithArrowKeys();
    }

    // True when the text reads as a number, not one partway typed like "-" or "2.".
    private static bool IsCompleteNumber(string typed, string suffix)
    {
        string number = typed.Trim();
        if (suffix != "" && number.EndsWith(suffix, StringComparison.Ordinal))
        {
            number = number[..^suffix.Length].Trim();
        }

        return !number.EndsWith('.') && double.TryParse(number, NumberStyles.Float,
            CultureInfo.InvariantCulture, out _);
    }

    /// <summary>
    /// Makes the field wrap around its range, for full-circle angles: stepping past the top
    /// (e.g. 360°) comes back at the bottom (0°), and below the bottom comes back at the top.
    /// The top itself counts as the bottom (360° is 0°). Returns the field, for chaining.
    /// </summary>
    public static SpinBox WithWrapAround(this SpinBox field)
    {
        field.AllowGreater = true;
        field.AllowLesser = true;
        field.ValueChanged += value =>
        {
            double span = field.MaxValue - field.MinValue;
            if (span > 0 && (value >= field.MaxValue || value < field.MinValue))
            {
                // Setting it fires ValueChanged again, now in range, so the world gets the
                // wrapped value too.
                field.Value = field.MinValue + ((value - field.MinValue) % span + span) % span;
            }
        };
        return field;
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
