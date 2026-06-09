#nullable enable

using System;
using System.Collections.Generic;
using CasaEngine.Editor.Styling;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Editor.Controls.Timeline;

internal sealed class TimelineTrackHeaderPanel : MGStackPanel
{
    private sealed class LaneRowState
    {
        public TimelineLane Lane { get; init; }

        public MGBorder Border { get; init; }

        public MGTextBlock Label { get; init; }

        public MGTextBox TextBox { get; init; }
    }

    private readonly MGWindow _window;
    private readonly TimelineControl _owner;
    private readonly Dictionary<Guid, LaneRowState> _rowsByLaneId = new();

    private string _text = string.Empty;
    private LaneRowState? _editingRow;
    private string _editingOriginalText = string.Empty;

    public string Text
    {
        get => _text;
        set => _text = value ?? string.Empty;
    }

    public event Action<TimelineLane, string>? LaneLabelEdited;

    public TimelineTrackHeaderPanel(MGWindow window, TimelineControl owner)
        : base(window, Orientation.Vertical)
    {
        _window = window;
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Spacing = 0;
    }

    public void RefreshRows()
    {
        CommitActiveEdit();
        TryRemoveAll();
        _rowsByLaneId.Clear();

        if (_owner.Model == null || _owner.Model.Lanes.Count == 0)
        {
            var fallbackBorder = CreateLaneBorder(isSelected: false);
            fallbackBorder.SetContent(CreateReadonlyLabel(_text));
            TryAddChild(fallbackBorder);
            return;
        }

        TryAddChild(CreateSpacer(TimelineControlMetrics.ViewportVerticalPadding));

        for (var index = 0; index < _owner.Model.Lanes.Count; index++)
        {
            TimelineLane lane = _owner.Model.Lanes[index];
            LaneRowState rowState = CreateLaneRow(lane);
            _rowsByLaneId[lane.Id] = rowState;
            TryAddChild(rowState.Border);
        }

        TryAddChild(CreateSpacer(TimelineControlMetrics.ViewportVerticalPadding));

        RefreshSelection();
    }

    public void RefreshSelection()
    {
        foreach (KeyValuePair<Guid, LaneRowState> pair in _rowsByLaneId)
        {
            bool isSelected = _owner.ViewState.SelectedLaneId == pair.Key;
            ApplySelectionVisual(pair.Value.Border, pair.Value.TextBox, isSelected);
        }
    }

    public float GetDesiredColumnWidth()
    {
        float desiredWidth = TimelineControlMetrics.TrackColumnWidth;
        if (_owner.Model == null || _owner.Model.Lanes.Count == 0)
        {
            return MathF.Max(desiredWidth, MeasureLabelWidth(_text));
        }

        for (var index = 0; index < _owner.Model.Lanes.Count; index++)
        {
            TimelineLane lane = _owner.Model.Lanes[index];
            desiredWidth = MathF.Max(desiredWidth, MeasureLabelWidth(lane.Label));
        }

        return desiredWidth;
    }

    private LaneRowState CreateLaneRow(TimelineLane lane)
    {
        var border = CreateLaneBorder(_owner.ViewState.SelectedLaneId == lane.Id);
        var label = CreateReadonlyLabel(lane.Label);
        var textBox = CreateEditorTextBox(lane.Label);
        border.SetContent(label);

        border.MouseHandler.LMBDoubleClickedInside += (_, e) =>
        {
            if (!lane.IsEditable)
            {
                return;
            }

            _owner.Focus(KeyboardFocusSource.Pointer);
            _owner.SetSelectedLaneId(lane.Id, true);
            _owner.SetSelectedEventId(null, true);
            BeginEdit(new LaneRowState { Lane = lane, Border = border, Label = label, TextBox = textBox });
            e.SetHandledBy(border, false);
        };

        textBox.KeyboardHandler.Pressed += (_, e) =>
        {
            if (_editingRow?.Lane.Id != lane.Id)
            {
                return;
            }

            switch (e.Key)
            {
                case Keys.Enter:
                    CommitActiveEdit();
                    e.SetHandledBy(textBox, false);
                    break;

                case Keys.Escape:
                    CancelActiveEdit();
                    e.SetHandledBy(textBox, false);
                    break;
            }
        };

        border.MouseHandler.LMBReleasedInside += (_, e) =>
        {
            CommitActiveEdit();
            _owner.Focus(KeyboardFocusSource.Pointer);
            _owner.SetSelectedLaneId(lane.Id, true);
            _owner.SetSelectedEventId(null, true);
            e.SetHandledBy(border, false);
        };

        border.MouseHandler.RMBReleasedInside += (_, e) =>
        {
            CommitActiveEdit();
            _owner.Focus(KeyboardFocusSource.Pointer);
            _owner.SetSelectedLaneId(lane.Id, true);
            _owner.SetSelectedEventId(null, true);

            if (!lane.IsEditable)
            {
                return;
            }

            MGContextMenu menu = new(_window, string.Empty);
            menu.AddButton("Rename track", _ => BeginEdit(new LaneRowState { Lane = lane, Border = border, Label = label, TextBox = textBox }));
            if (menu.TryOpenContextMenu(e.Position))
            {
                e.SetHandledBy(menu, false);
            }
        };

        return new LaneRowState
        {
            Lane = lane,
            Border = border,
            Label = label,
            TextBox = textBox,
        };
    }

    private MGBorder CreateLaneBorder(bool isSelected)
    {
        var border = new MGBorder(
            _window,
            new Thickness(0),
            new MGUniformBorderBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBorder)))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = TimelineControlMetrics.TrackRowHeight,
            PreferredHeight = TimelineControlMetrics.TrackRowHeight,
            Padding = new Thickness(TimelineControlMetrics.HeaderPadding, 0, TimelineControlMetrics.HeaderPadding, 0),
        };

        ApplySelectionVisual(border, null, isSelected);
        return border;
    }

    private MGTextBlock CreateReadonlyLabel(string text)
    {
        return new MGTextBlock(_window, text ?? string.Empty)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            WrapText = false,
            Padding = new Thickness(0),
        };
    }

    private MGTextBox CreateEditorTextBox(string text)
    {
        var textBox = new MGTextBox(_window, CharacterLimit: 256)
        {
            AcceptsReturn = false,
            AcceptsTab = false,
            HasStableTextFootprint = true,
            IsReadonly = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            PreferredWidth = (int)MathF.Ceiling(MeasureLabelWidth(text) - (TimelineControlMetrics.HeaderPadding * 2f)),
            MinWidth = (int)MathF.Ceiling(MeasureLabelWidth(text) - (TimelineControlMetrics.HeaderPadding * 2f)),
        };
        textBox.SetText(text ?? string.Empty);
        return textBox;
    }

    private void ApplySelectionVisual(MGBorder border, MGTextBox? textBox, bool isSelected)
    {
        border.BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(
            isSelected
                ? EditorThemePalette.AccentSelection * 0.25f
                : EditorThemePalette.PreviewSurfaceBackground));
    }

    private void BeginEdit(LaneRowState row)
    {
        CommitActiveEdit();

        _editingRow = row;
        _editingOriginalText = row.TextBox.Text ?? string.Empty;
        row.TextBox.IsReadonly = false;
        row.Border.SetContent(row.TextBox);
        row.TextBox.RequestFocus();

        if (_window.Desktop != null)
        {
            _window.Desktop.FocusedKeyboardHandlerChanged -= OnFocusedKeyboardHandlerChanged;
            _window.Desktop.FocusedKeyboardHandlerChanged += OnFocusedKeyboardHandlerChanged;
        }
    }

    private void CommitActiveEdit()
    {
        if (_editingRow == null)
        {
            return;
        }

        LaneRowState editingRow = _editingRow;
        _editingRow = null;
        editingRow.TextBox.IsReadonly = true;
        editingRow.Label.Text = editingRow.TextBox.Text ?? string.Empty;
        editingRow.Border.SetContent(editingRow.Label);

        if (_window.Desktop != null)
        {
            _window.Desktop.FocusedKeyboardHandlerChanged -= OnFocusedKeyboardHandlerChanged;
        }

        string newText = editingRow.TextBox.Text?.Trim() ?? string.Empty;
        if (string.Equals(newText, _editingOriginalText, StringComparison.Ordinal))
        {
            editingRow.TextBox.SetText(_editingOriginalText, true);
            return;
        }

        LaneLabelEdited?.Invoke(editingRow.Lane, newText);
    }

    private void CancelActiveEdit()
    {
        if (_editingRow == null)
        {
            return;
        }

        LaneRowState editingRow = _editingRow;
        _editingRow = null;
        editingRow.TextBox.IsReadonly = true;
        editingRow.TextBox.SetText(_editingOriginalText, true);
        editingRow.Label.Text = _editingOriginalText;
        editingRow.Border.SetContent(editingRow.Label);

        if (_window.Desktop != null)
        {
            _window.Desktop.FocusedKeyboardHandlerChanged -= OnFocusedKeyboardHandlerChanged;
        }
    }

    private void OnFocusedKeyboardHandlerChanged(object? sender, EventArgs<MGElement>? e)
    {
        if (_editingRow == null)
        {
            return;
        }

        if (ReferenceEquals(e?.PreviousValue, _editingRow.TextBox) && !ReferenceEquals(e?.NewValue, _editingRow.TextBox))
        {
            CommitActiveEdit();
        }
    }

    private float MeasureLabelWidth(string text)
    {
        if (!TryResolveFont(out ITextMeasurementEngine textEngine, out ResolvedFont font, out float scale) || !font.IsAvailable)
        {
            return TimelineControlMetrics.TrackColumnWidth;
        }

        string safeText = string.IsNullOrWhiteSpace(text) ? _text : text;
        Vector2 textSize = textEngine.MeasureText(font, safeText);
        return MathF.Max(
            TimelineControlMetrics.TrackColumnWidth,
            textSize.X + (TimelineControlMetrics.HeaderPadding * 2f) + 24f);
    }

    private bool TryResolveFont(out ITextMeasurementEngine textEngine, out ResolvedFont font, out float scale)
    {
        textEngine = GetTextEngine();
        font = textEngine.ResolveFont(new FontSpec(ParentWindow.Desktop.DefaultFontFamily, TimelineControlMetrics.LabelFontSize, CustomFontStyles.Normal));
        scale = font.SuggestedScale;
        return font.IsAvailable;
    }

    private MGBorder CreateSpacer(int height)
    {
        return new MGBorder(_window)
        {
            BorderThickness = new Thickness(0),
            MinHeight = height,
            PreferredHeight = height,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBackground)),
        };
    }
}