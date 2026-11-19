#nullable enable

using System;
using System.Globalization;
using CasaEngine.EditorServices.Cutscenes;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls.Cutscenes;

/// <summary>
/// A text box with minus and plus buttons for a number of a cutscene action (ADR-0136). It never writes to the asset: it
/// raises <see cref="CommitRequested"/> and the owner parses, checks and writes the text. The typed text is committed by
/// Enter or when the focus leaves the box; a button changes the text by one step, within the limits, and commits at once.
/// </summary>
internal sealed class CutsceneNumberField
{
    private readonly MGTextBox _textBox;
    private readonly MGButton? _minusButton;
    private readonly MGButton? _plusButton;
    private readonly MGElement _element;
    private readonly CutsceneTextCommitBinding _binding;
    private readonly bool _isInteger;
    private readonly double? _minimum;
    private readonly double? _maximum;
    private readonly bool _isMinimumExclusive;
    private readonly double _step;
    private string _committedText = string.Empty;
    private bool _isUpdating;

    /// <param name="window">The window the controls belong to.</param>
    /// <param name="isInteger">True to step and write whole numbers.</param>
    /// <param name="minimum">The lowest accepted value, or null.</param>
    /// <param name="maximum">The highest accepted value, or null.</param>
    /// <param name="isMinimumExclusive">True when the value must be greater than the minimum.</param>
    /// <param name="step">What a button adds or removes.</param>
    /// <param name="showButtons">False for a compact field without buttons (the components of a vector).</param>
    public CutsceneNumberField(
        MGWindow window,
        bool isInteger,
        double? minimum,
        double? maximum,
        bool isMinimumExclusive,
        double step,
        bool showButtons)
    {
        _isInteger = isInteger;
        _minimum = minimum;
        _maximum = maximum;
        _isMinimumExclusive = isMinimumExclusive;
        _step = step;

        _textBox = new MGTextBox(window, CharacterLimit: 24)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            HasStableTextFootprint = true,
            AcceptsReturn = false,
            AcceptsTab = false,
            MinWidth = 40,
        };

        _binding = new CutsceneTextCommitBinding(window, _textBox, OnCommit);

        if (!showButtons)
        {
            _element = _textBox;
            return;
        }

        _minusButton = CreateButton(window, "-", () => Step(-1));
        _plusButton = CreateButton(window, "+", () => Step(1));

        var dock = new MGDockPanel(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        dock.TryAddChild(_plusButton, Dock.Right);
        dock.TryAddChild(_minusButton, Dock.Right);
        dock.TryAddChild(_textBox, Dock.Left);
        _element = dock;
    }

    /// <summary>Raised when the text must be written: true for Enter and for a button, false when the focus left the box.</summary>
    public event Action<CutsceneNumberField, bool>? CommitRequested;

    public MGElement Element => _element;

    public MGTextBox TextBox => _textBox;

    public MGButton? MinusButton => _minusButton;

    public MGButton? PlusButton => _plusButton;

    public string Text => _textBox.Text ?? string.Empty;

    /// <summary>True when the text differs from the last text the owner gave to <see cref="SetText"/>.</summary>
    public bool HasPendingText => !string.Equals(Text, _committedText, StringComparison.Ordinal);

    /// <summary>Shows the text of the value of the asset, which is also the text that counts as committed.</summary>
    public void SetText(string text)
    {
        _committedText = text;
        if (string.Equals(Text, text, StringComparison.Ordinal))
        {
            return;
        }

        _isUpdating = true;
        _textBox.SetText(text);
        _isUpdating = false;
    }

    /// <summary>Raises <see cref="CommitRequested"/> for the typed text, when there is one waiting.</summary>
    public void Flush()
    {
        if (HasPendingText)
        {
            CommitRequested?.Invoke(this, false);
        }
    }

    /// <summary>
    /// What a button does: the value of the text (0 when the text is not a number) moves by one step, stays within the limits,
    /// and is committed. A step down that would leave the limits does nothing.
    /// </summary>
    public void Step(int direction)
    {
        if (!double.TryParse(Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double current)
            || !double.IsFinite(current))
        {
            current = 0;
        }

        double next = Math.Round(current + direction * _step, 6);
        if (_minimum.HasValue && (next < _minimum.Value || (_isMinimumExclusive && next <= _minimum.Value)))
        {
            if (direction < 0)
            {
                return;
            }

            // A step up from a text below the limit lands on the lowest value the field accepts.
            next = _isMinimumExclusive ? Math.Round(_minimum.Value + _step, 6) : _minimum.Value;
        }

        if (_maximum.HasValue && next > _maximum.Value)
        {
            next = _maximum.Value;
        }

        string text = _isInteger
            ? ((long)Math.Round(next)).ToString(CultureInfo.InvariantCulture)
            : ((float)next).ToString("R", CultureInfo.InvariantCulture);
        if (string.Equals(text, Text, StringComparison.Ordinal))
        {
            return;
        }

        _isUpdating = true;
        _textBox.SetText(text);
        _isUpdating = false;
        CommitRequested?.Invoke(this, true);
    }

    /// <summary>Stops listening to the keyboard focus. Call it when the field is removed.</summary>
    public void Detach() => _binding.Detach();

    /// <summary>
    /// Checks the limits declared by the metadata of a property on a parsed value (a number, an integer or a byte). Returns a
    /// message when the value is outside them, and null otherwise.
    /// </summary>
    public static string? GetBoundsError(CutsceneActionProperty property, object? value)
    {
        if (value == null || property.Kind is not (CutsceneActionPropertyKind.Number or CutsceneActionPropertyKind.Integer or CutsceneActionPropertyKind.Byte))
        {
            return null;
        }

        double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (property.Minimum.HasValue)
        {
            double minimum = property.Minimum.Value;
            if (property.IsMinimumExclusive ? number <= minimum : number < minimum)
            {
                string how = property.IsMinimumExclusive ? "greater than" : "at least";
                return string.Create(CultureInfo.InvariantCulture, $"{property.Key} must be {how} {minimum:0.###}.");
            }
        }

        if (property.Maximum.HasValue && number > property.Maximum.Value)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{property.Key} must be at most {property.Maximum.Value:0.###}.");
        }

        return null;
    }

    private void OnCommit(bool isEnter)
    {
        if (_isUpdating)
        {
            return;
        }

        CommitRequested?.Invoke(this, isEnter);
    }

    private static MGButton CreateButton(MGWindow window, string label, Action onClick)
    {
        var button = new MGButton(window, _ => onClick())
        {
            Padding = new Thickness(9, 2, 9, 2),
            Margin = new Thickness(3, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.SetContent(new MGTextBlock(window, label, null, null, false)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return button;
    }
}
