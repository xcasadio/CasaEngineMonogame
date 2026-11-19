#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using CasaEngine.Editor.Controls.Cutscenes;
using CasaEngine.Editor.Styling;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MonoGame.Extended;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// The Inspector view of a cutscene document (ADR-0136): the properties of the selected action in typed fields, laid out in
/// a two-column form, with the messages that concern the action under them. Without a selection it shows the cutscene: its
/// file, its validation and the state of the editor play mode. It never edits the asset itself: every write goes through
/// <see cref="CutsceneAssetInspectorPanel.SetProperty"/> or <see cref="CutsceneAssetInspectorPanel.SetProperties"/>.
/// </summary>
/// <remarks>
/// The fields are built when the selection changes and refilled in place when the document changes, so that a field being
/// edited keeps the focus; nothing is rebuilt per frame. The text typed in a field and not yet validated is written before
/// the fields are replaced, for the action they were built for.
/// </remarks>
internal sealed class CutsceneDetailsPanel
{
    private const int LabelWidth = 132;
    private const int ClearSlotWidth = 28;
    private const int ColumnSpacing = 8;
    private const string NoneItem = "(none)";

    private readonly MGWindow _window;
    private readonly List<DetailRow> _rows = new();

    private MGScrollViewer? _root;
    private MGStackPanel? _content;
    private MGStackPanel? _messagesStack;
    private MGTextBlock? _pathText;
    private CutsceneAssetInspectorPanel? _document;
    private CutsceneActionData? _builtFor;
    private bool _isUpdating;
    private bool _isFlushing;

    public CutsceneDetailsPanel(MGWindow window)
    {
        _window = window;
    }

    /// <summary>The document the details show, or null.</summary>
    internal CutsceneAssetInspectorPanel? Document => _document;

    /// <summary>The action the fields were built for, or null in the cutscene view.</summary>
    internal CutsceneActionData? BuiltFor => _builtFor;

    internal IReadOnlyList<DetailRow> Rows => _rows;

    internal MGStackPanel? Content => _content;

    internal MGStackPanel? MessagesStack => _messagesStack;

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _content = new MGStackPanel(_window, Orientation.Vertical)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(10, 8, 10, 10),
            Spacing = 6,
        };

        _root = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _root.SetContent(_content);

        Rebuild();
        return _root;
    }

    /// <summary>
    /// Shows the details of a document, or nothing with null. What was typed and not validated is written to the previous
    /// document first, and the panel keeps no subscription on it afterwards.
    /// </summary>
    public void SetEditorPanel(CutsceneAssetInspectorPanel? document)
    {
        if (ReferenceEquals(_document, document))
        {
            return;
        }

        if (_document != null)
        {
            FlushPendingText();
            _document.DocumentChanged -= OnDocumentChanged;
            _document.SelectionChanged -= OnSelectionChanged;
            _document.RuntimeDisplayChanged -= OnRuntimeDisplayChanged;
        }

        _document = document;

        if (_document != null)
        {
            _document.DocumentChanged += OnDocumentChanged;
            _document.SelectionChanged += OnSelectionChanged;
            _document.RuntimeDisplayChanged += OnRuntimeDisplayChanged;
        }

        Rebuild();
    }

    /// <summary>Writes the text typed in the fields and not yet validated, for the action they were built for.</summary>
    internal void FlushPendingText()
    {
        _isFlushing = true;
        try
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                _rows[index].Flush();
            }
        }
        finally
        {
            _isFlushing = false;
        }
    }

    /// <summary>Writes the text typed and not yet validated, then shows the values the document now holds.</summary>
    internal void CommitPendingText()
    {
        FlushPendingText();
        Rebuild();
    }

    private void OnSelectionChanged(CutsceneAssetInspectorPanel document)
    {
        CommitPendingText();
    }

    private void OnDocumentChanged(CutsceneAssetInspectorPanel document)
    {
        if (_isFlushing)
        {
            // The caller rebuilds the fields once everything pending is written.
            return;
        }

        var selected = document.SelectedAction;
        if (selected != null && ReferenceEquals(selected, _builtFor))
        {
            RefreshValues(selected);
            return;
        }

        Rebuild();
    }

    private void OnRuntimeDisplayChanged(CutsceneAssetInspectorPanel document)
    {
        if (document.SelectedAction == null)
        {
            Rebuild();
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Building
    // ---------------------------------------------------------------------------------------------------------------------

    private void Rebuild()
    {
        if (_content == null)
        {
            return;
        }

        for (int index = 0; index < _rows.Count; index++)
        {
            _rows[index].Detach();
        }

        _rows.Clear();
        _content.TryRemoveAll();
        _messagesStack = null;
        _pathText = null;
        _builtFor = null;

        var session = _document?.Session;
        if (_document == null || session == null)
        {
            AddNote("No cutscene is open.");
            return;
        }

        var selected = _document.SelectedAction;
        if (selected == null)
        {
            BuildCutsceneView(_document, session);
            return;
        }

        BuildActionView(_document, session, selected);
    }

    private void BuildCutsceneView(CutsceneAssetInspectorPanel document, CutsceneEditSession session)
    {
        var readOnly = document.ReadOnlyDocument;
        AddTitle(readOnly?.AssetName ?? "Cutscene");
        AddSecondary(string.IsNullOrWhiteSpace(document.LoadedRelativePath) ? "No source path." : document.LoadedRelativePath!);
        AddNote("Select an action in the Hierarchy\nto edit its properties.");

        AddSection("Validation");
        var validationStack = NewStack();
        _content!.TryAddChild(validationStack);
        bool any = false;
        if (readOnly != null)
        {
            for (int index = 0; index < readOnly.ValidationMessages.Count; index++)
            {
                var message = readOnly.ValidationMessages[index];
                AddMessage(validationStack, $"{message.Severity}: {message.Path} - {message.Message}");
                any = true;
            }
        }

        for (int index = 0; index < session.LoadLosses.Count; index++)
        {
            var loss = session.LoadLosses[index];
            AddMessage(validationStack, $"Load loss: {loss.Path} ({loss.Key}) - {loss.Message}");
            any = true;
        }

        if (!any)
        {
            AddMessage(validationStack, "No validation warnings or errors.", isProblem: false);
        }

        AddSection("Editor play mode");
        if (readOnly == null)
        {
            return;
        }

        var grid = NewFormGrid();
        int row = 0;
        row = AddInfoRow(grid, row, "Runtime state", readOnly.RuntimeState.ToString());
        row = AddInfoRow(grid, row, "Coroutines", readOnly.ActiveCoroutines.Count.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(readOnly.ActiveActionType))
        {
            row = AddInfoRow(grid, row, "Active action", readOnly.ActiveActionType!);
            row = AddInfoRow(grid, row, "Action entity", string.IsNullOrWhiteSpace(readOnly.ActiveActionEntityName) ? "(none)" : readOnly.ActiveActionEntityName!);
            row = AddInfoRow(grid, row, "Action state", string.IsNullOrWhiteSpace(readOnly.ActiveActionState) ? "(none)" : readOnly.ActiveActionState!);
            AddInfoRow(grid, row, "Stop reason", string.IsNullOrWhiteSpace(readOnly.ActiveActionStopReason) ? "(none)" : readOnly.ActiveActionStopReason!);
        }

        _content.TryAddChild(grid);
    }

    private void BuildActionView(CutsceneAssetInspectorPanel document, CutsceneEditSession session, CutsceneActionData action)
    {
        _builtFor = action;

        AddTitle(action.Type);
        _pathText = AddSecondary(GetPathText(session, action));

        if (action is UnknownCutsceneActionData)
        {
            AddNote("Unknown action type: it cannot be edited and a save is refused.");
        }

        var properties = CutsceneActionPropertyEditor.GetProperties(action);
        if (properties.Count == 0 && action is not UnknownCutsceneActionData)
        {
            AddNote("This action has no property of its own.");
        }

        AddSection("Properties");
        var form = NewStack();
        form.Spacing = 8;
        _content!.TryAddChild(form);

        for (int index = 0; index < properties.Count; index++)
        {
            var property = properties[index];
            DetailRow? row;
            if (action is FadeScreenCutsceneActionData && property.Key == "r")
            {
                row = new RgbColorRow(this, action);
            }
            else if (action is FadeScreenCutsceneActionData && property.Key is "g" or "b")
            {
                continue;
            }
            else
            {
                row = CreateRow(action, property);
            }

            _rows.Add(row);
            form.TryAddChild(row.Element);
        }

        AddSection("Messages");
        _messagesStack = NewStack();
        _content.TryAddChild(_messagesStack);
        FillMessages(session, action);
    }

    private DetailRow CreateRow(CutsceneActionData action, CutsceneActionProperty property)
    {
        if (property.Kind is CutsceneActionPropertyKind.String or CutsceneActionPropertyKind.Guid)
        {
            // A list with nothing to choose from is of no use: the reference then stays a text field (ADR-0136).
            if (CutsceneReferenceOptions.IsAssetReference(property.Semantic))
            {
                var options = CutsceneReferenceOptions.Build(property.Semantic, AssetCatalog.AssetInfos);
                if (options.Count > 0)
                {
                    return new ChoiceRow(this, action, property, options, null);
                }
            }
            else if (property.Semantic == CutsceneActionPropertySemantic.Entity && _document?.Preview?.World != null)
            {
                var names = new List<string>(_document.GetPreviewEntityNames());
                if (names.Count > 0)
                {
                    return new ChoiceRow(this, action, property, null, names);
                }
            }
        }

        switch (property.Kind)
        {
            case CutsceneActionPropertyKind.Number:
            case CutsceneActionPropertyKind.Integer:
            case CutsceneActionPropertyKind.Byte:
                return new NumberRow(this, action, property);
            case CutsceneActionPropertyKind.Boolean:
                return new BooleanRow(this, action, property);
            case CutsceneActionPropertyKind.Enum:
                return new EnumRow(this, action, property);
            case CutsceneActionPropertyKind.Color:
                return new ColorRow(this, action, property);
            case CutsceneActionPropertyKind.Vector3:
                return new VectorRow(this, action, property);
            default:
                return new TextRow(this, action, property);
        }
    }

    /// <summary>Fills the fields from the asset, without writing anything, and refreshes the texts that depend on the asset.</summary>
    private void RefreshValues(CutsceneActionData action)
    {
        var session = _document?.Session;
        if (session == null)
        {
            return;
        }

        _isUpdating = true;
        try
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                _rows[index].Refresh();
            }
        }
        finally
        {
            _isUpdating = false;
        }

        _pathText?.SetText(GetPathText(session, action));
        FillMessages(session, action);
    }

    private void FillMessages(CutsceneEditSession session, CutsceneActionData action)
    {
        if (_messagesStack == null)
        {
            return;
        }

        _messagesStack.TryRemoveAll();
        string? path = session.Asset.RootAction == null ? null : CutsceneActionTree.GetPath(session.Asset.RootAction, action);
        bool any = false;

        var readOnly = _document?.ReadOnlyDocument;
        if (readOnly != null && path != null)
        {
            for (int index = 0; index < readOnly.ValidationMessages.Count; index++)
            {
                var message = readOnly.ValidationMessages[index];
                if (string.Equals(message.Path, path, StringComparison.Ordinal))
                {
                    AddMessage(_messagesStack, $"{message.Severity}: {message.Message}");
                    any = true;
                }
            }
        }

        var formatErrors = CutsceneActionPropertyEditor.GetFormatErrors(action);
        if (formatErrors != null)
        {
            foreach (var pair in formatErrors)
            {
                AddMessage(_messagesStack, $"Format error: {pair.Key} - {pair.Value}");
                any = true;
            }
        }

        for (int index = 0; index < session.LoadLosses.Count; index++)
        {
            var loss = session.LoadLosses[index];
            if (ReferenceEquals(loss.Action, action))
            {
                AddMessage(_messagesStack, $"Load loss: {loss.Key} - {loss.Message}");
                any = true;
            }
        }

        if (!any)
        {
            AddMessage(_messagesStack, "No validation message for this action.", isProblem: false);
        }
    }

    private static string GetPathText(CutsceneEditSession session, CutsceneActionData action)
    {
        var root = session.Asset.RootAction;
        return (root == null ? null : CutsceneActionTree.GetPath(root, action)) ?? "(not in the cutscene)";
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Writing
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>True when the property holds a format error or a load loss: Enter then writes even an unchanged text.</summary>
    private bool HasProblem(CutsceneActionData action, string key)
    {
        var formatErrors = CutsceneActionPropertyEditor.GetFormatErrors(action);
        if (formatErrors != null && formatErrors.ContainsKey(key))
        {
            return true;
        }

        var losses = _document?.Session?.LoadLosses;
        if (losses != null)
        {
            for (int index = 0; index < losses.Count; index++)
            {
                if (ReferenceEquals(losses[index].Action, action) && string.Equals(losses[index].Key, key, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Parses a text, checks the limits and writes the value. Returns the reason when it is refused, otherwise null.</summary>
    private string? WriteText(CutsceneActionData action, CutsceneActionProperty property, string text)
    {
        if (_document == null)
        {
            return "No cutscene is open.";
        }

        if (!property.TryParse(text, out object? value, out string error))
        {
            return error;
        }

        return CutsceneNumberField.GetBoundsError(property, value) ?? _document.SetProperty(action, property, value);
    }

    private string? WriteValue(CutsceneActionData action, CutsceneActionProperty property, object? value)
    {
        return _document == null ? "No cutscene is open." : _document.SetProperty(action, property, value);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Layout helpers
    // ---------------------------------------------------------------------------------------------------------------------

    private MGStackPanel NewStack() => new(_window, Orientation.Vertical)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Spacing = 3,
    };

    private MGGrid NewFormGrid()
    {
        var grid = new MGGrid(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ColumnSpacing = ColumnSpacing,
            RowSpacing = 4,
        };
        grid.AddColumn(GridLength.CreatePixelLength(LabelWidth));
        grid.AddColumn(GridLength.CreateWeightedLength(1));
        return grid;
    }

    private void AddTitle(string text)
    {
        _content!.TryAddChild(new MGTextBlock(_window, "[b]" + Escape(text) + "[/b]")
        {
            FontSize = 16,
            WrapText = true,
            Opacity = EditorThemePalette.PrimaryHeaderOpacity,
        });
    }

    private MGTextBlock AddSecondary(string text)
    {
        var block = new MGTextBlock(_window, text, null, null, false)
        {
            WrapText = true,
            Opacity = EditorThemePalette.SecondaryTextOpacity,
        };
        _content!.TryAddChild(block);
        return block;
    }

    private void AddNote(string text)
    {
        _content!.TryAddChild(new MGTextBlock(_window, text, null, null, false)
        {
            WrapText = true,
            Margin = new Thickness(0, 2, 0, 2),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
        });
    }

    private void AddSection(string title)
    {
        _content!.TryAddChild(new MGTextBlock(_window, "[b]" + Escape(title) + "[/b]")
        {
            Margin = new Thickness(0, 10, 0, 2),
            Opacity = EditorThemePalette.SectionHeaderOpacity,
        });
    }

    private void AddMessage(MGStackPanel stack, string text, bool isProblem = true)
    {
        stack.TryAddChild(new MGTextBlock(_window, text, isProblem ? Color.Orange : null, null, false)
        {
            WrapText = true,
            Opacity = isProblem ? 1f : EditorThemePalette.SecondaryTextOpacity,
        });
    }

    private int AddInfoRow(MGGrid grid, int rowIndex, string label, string value)
    {
        grid.AddRow(GridLength.Auto);
        grid.TryAddChild(rowIndex, 0, new MGTextBlock(_window, label, null, null, false)
        {
            Opacity = EditorThemePalette.SectionLabelOpacity,
            VerticalAlignment = VerticalAlignment.Top,
        });
        grid.TryAddChild(rowIndex, 1, new MGTextBlock(_window, value, null, null, false)
        {
            WrapText = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        return rowIndex + 1;
    }

    private static string Escape(string value) => value.Replace("[", "[[").Replace("]", "]]");

    /// <summary>
    /// The label of a property: its key with spaces and a capital, such as "Duration (s)". A trailing "_seconds" becomes
    /// "(s)" and "stopping_distance" becomes "Stop distance", so that no label is wider than the label column.
    /// </summary>
    internal static string GetLabel(string key)
    {
        if (key.Length == 0)
        {
            return key;
        }

        if (key == "stopping_distance")
        {
            key = "stop_distance";
        }

        const string SecondsSuffix = "_seconds";
        string spaced = key.EndsWith(SecondsSuffix, StringComparison.Ordinal) && key.Length > SecondsSuffix.Length
            ? key.Substring(0, key.Length - SecondsSuffix.Length).Replace('_', ' ') + " (s)"
            : key.Replace('_', ' ');
        return char.ToUpperInvariant(spaced[0]) + spaced.Substring(1);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Rows
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>One line of the form: a label, a field, a button that clears a nullable value, and the reason of a refusal.</summary>
    internal abstract class DetailRow
    {
        private readonly MGStackPanel _element;
        private readonly MGTextBlock _error;

        protected DetailRow(CutsceneDetailsPanel owner, CutsceneActionData action, string label)
        {
            Owner = owner;
            Action = action;

            _element = new MGStackPanel(owner._window, Orientation.Vertical)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Spacing = 2,
            };
            Grid = owner.NewFormGrid();
            Grid.AddRow(GridLength.Auto);
            Grid.TryAddChild(0, 0, new MGTextBlock(owner._window, label, null, null, false)
            {
                Opacity = EditorThemePalette.SectionLabelOpacity,
                VerticalAlignment = VerticalAlignment.Center,
            });

            _error = new MGTextBlock(owner._window, string.Empty, Color.Orange, null, false)
            {
                WrapText = true,
                Margin = new Thickness(LabelWidth + ColumnSpacing, 0, 0, 0),
                Visibility = Visibility.Collapsed,
            };

            _element.TryAddChild(Grid);
            _element.TryAddChild(_error);
        }

        protected CutsceneDetailsPanel Owner { get; }

        protected CutsceneActionData Action { get; }

        protected MGGrid Grid { get; }

        /// <summary>The line to add to the form.</summary>
        public MGElement Element => _element;

        /// <summary>The reason the last write was refused, or empty.</summary>
        public string ErrorText => _error.Text ?? string.Empty;

        /// <summary>The property the row edits.</summary>
        public abstract string Key { get; }

        /// <summary>The primary control of the row, for tests.</summary>
        public abstract MGElement Field { get; }

        /// <summary>Shows the value of the asset in the field, without writing.</summary>
        public void Refresh()
        {
            ShowError(null);
            RefreshField();
        }

        protected abstract void RefreshField();

        /// <summary>Writes the text typed and not yet validated, if any.</summary>
        public virtual void Flush()
        {
        }

        public virtual void Detach()
        {
        }

        protected void SetField(MGElement field)
        {
            field.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.TryAddChild(0, 1, field);
        }

        /// <summary>After a write: the field shows the asset again, with the reason when the write was refused.</summary>
        protected void Complete(string? refusal)
        {
            Owner._isUpdating = true;
            try
            {
                RefreshField();
            }
            finally
            {
                Owner._isUpdating = false;
            }

            ShowError(refusal);
        }

        protected void ShowError(string? message)
        {
            bool has = !string.IsNullOrEmpty(message);
            _error.SetText(message ?? string.Empty);
            _error.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>A small button that sets a nullable property to "no value".</summary>
        protected MGButton CreateClearButton(Action onClick)
        {
            var button = new MGButton(Owner._window, _ => onClick())
            {
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(2, 0, 0, 0),
                PreferredWidth = ClearSlotWidth,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var toolTip = new MGToolTip(Owner._window, button, 110, 30)
            {
                ShowDelayOverride = TimeSpan.FromMilliseconds(300),
            };
            toolTip.SetContent(new MGTextBlock(Owner._window, "Clear value", null, null, false)
            {
                Margin = new Thickness(6, 4, 6, 4),
            });
            button.ToolTip = toolTip;
            button.SetContent(new MGTextBlock(Owner._window, "x", null, null, false)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            return button;
        }

        /// <summary>The field with the clear button of a nullable property, or a blank of the same width when it has none.</summary>
        protected MGElement WithClearButton(CutsceneActionProperty property, MGElement field, Action onClear)
        {
            if (property.Kind is CutsceneActionPropertyKind.String or CutsceneActionPropertyKind.StringList)
            {
                return field;
            }

            var dock = new MGDockPanel(Owner._window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            if (property.IsNullable)
            {
                ClearButton = CreateClearButton(onClear);
                dock.TryAddChild(ClearButton, Dock.Right);
            }
            else
            {
                // A hidden clear button keeps its width, so that the fields and the buttons of all the rows share their columns.
                var blank = CreateClearButton(() => { });
                blank.ToolTip = null;
                blank.Visibility = Visibility.Hidden;
                dock.TryAddChild(blank, Dock.Right);
            }

            dock.TryAddChild(field, Dock.Left);
            return dock;
        }

        /// <summary>The button that clears a nullable value, or null.</summary>
        public MGButton? ClearButton { get; private set; }
    }

    /// <summary>A number, an integer or a byte: a field with minus and plus buttons within the limits of the validator.</summary>
    internal sealed class NumberRow : DetailRow
    {
        private readonly CutsceneActionProperty _property;
        private readonly CutsceneNumberField _field;

        public NumberRow(CutsceneDetailsPanel owner, CutsceneActionData action, CutsceneActionProperty property)
            : base(owner, action, GetLabel(property.Key))
        {
            _property = property;

            bool isWhole = property.Kind != CutsceneActionPropertyKind.Number;
            double? minimum = property.Minimum ?? (property.Kind == CutsceneActionPropertyKind.Byte ? 0 : null);
            double? maximum = property.Maximum ?? (property.Kind == CutsceneActionPropertyKind.Byte ? 255 : null);
            double step = isWhole ? 1 : maximum is <= 1 ? 0.1 : 1;
            _field = new CutsceneNumberField(owner._window, isWhole, minimum, maximum, property.IsMinimumExclusive, step, showButtons: true);
            _field.CommitRequested += OnCommitRequested;
            _field.SetText(property.GetText(action));
            SetField(WithClearButton(property, _field.Element, Clear));
        }

        public override string Key => _property.Key;

        public override MGElement Field => _field.Element;

        public CutsceneNumberField NumberField => _field;

        public override void Flush() => _field.Flush();

        public override void Detach()
        {
            _field.CommitRequested -= OnCommitRequested;
            _field.Detach();
        }

        protected override void RefreshField() => _field.SetText(_property.GetText(Action));

        private void OnCommitRequested(CutsceneNumberField field, bool isEnter)
        {
            if (Owner._isUpdating)
            {
                return;
            }

            // Enter writes a field with a problem even when its text is unchanged: that is how the problem is cleared.
            if (!field.HasPendingText && !(isEnter && Owner.HasProblem(Action, _property.Key)))
            {
                return;
            }

            Complete(Owner.WriteText(Action, _property, field.Text));
        }

        public void Clear() => Complete(Owner.WriteValue(Action, _property, null));
    }

    /// <summary>A flag: a check box.</summary>
    internal sealed class BooleanRow : DetailRow
    {
        private readonly CutsceneActionProperty _property;
        private readonly MGCheckBox _checkBox;

        public BooleanRow(CutsceneDetailsPanel owner, CutsceneActionData action, CutsceneActionProperty property)
            : base(owner, action, GetLabel(property.Key))
        {
            _property = property;
            _checkBox = new MGCheckBox(owner._window)
            {
                IsChecked = (bool?)property.GetValue(action) ?? false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _checkBox.OnCheckStateChanged += OnCheckStateChanged;
            SetField(_checkBox);
            _checkBox.HorizontalAlignment = HorizontalAlignment.Left;
        }

        public override string Key => _property.Key;

        public override MGElement Field => _checkBox;

        public MGCheckBox CheckBox => _checkBox;

        public override void Detach() => _checkBox.OnCheckStateChanged -= OnCheckStateChanged;

        protected override void RefreshField() => _checkBox.IsChecked = (bool?)_property.GetValue(Action) ?? false;

        private void OnCheckStateChanged(object? sender, MGUI.Shared.Helpers.EventArgs<bool?> args)
        {
            if (Owner._isUpdating)
            {
                return;
            }

            Complete(Owner.WriteValue(Action, _property, args.NewValue == true));
        }
    }

    /// <summary>An enumeration: a drop-down list of its names, with "(none)" when the property can be empty.</summary>
    internal sealed class EnumRow : DetailRow
    {
        private readonly CutsceneActionProperty _property;
        private readonly MGComboBox<string> _combo;

        public EnumRow(CutsceneDetailsPanel owner, CutsceneActionData action, CutsceneActionProperty property)
            : base(owner, action, GetLabel(property.Key))
        {
            _property = property;

            var names = new List<string>();
            if (property.IsNullable)
            {
                names.Add(NoneItem);
            }

            names.AddRange(Enum.GetNames(property.ValueType));

            _combo = new MGComboBox<string>(owner._window)
            {
                MinWidth = 100,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _combo.DropdownItemTemplate = item =>
            {
                var button = _combo.CreateDefaultDropdownButton();
                button.SetContent(item);
                return button;
            };
            _combo.SelectedItemTemplate = item => new MGTextBlock(owner._window, item, null, null, false)
            {
                Padding = new Thickness(4, 1, 4, 1),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _combo.SetItemsSource(names);
            _combo.SelectedItem = GetItemName();
            _combo.SelectedItemChanged += OnSelectedItemChanged;
            SetField(_combo);
        }

        public override string Key => _property.Key;

        public override MGElement Field => _combo;

        public MGComboBox<string> Combo => _combo;

        public override void Detach() => _combo.SelectedItemChanged -= OnSelectedItemChanged;

        protected override void RefreshField() => _combo.SelectedItem = GetItemName();

        private string GetItemName()
        {
            object? value = _property.GetValue(Action);
            return value == null ? NoneItem : value.ToString() ?? NoneItem;
        }

        private void OnSelectedItemChanged(object? sender, MGUI.Shared.Helpers.EventArgs<string> args)
        {
            if (Owner._isUpdating || args.NewValue == null)
            {
                return;
            }

            string text = args.NewValue == NoneItem ? string.Empty : args.NewValue;
            Complete(Owner.WriteText(Action, _property, text));
        }
    }

    /// <summary>A colour: a swatch that opens the colour window, with its value as text.</summary>
    internal sealed class ColorRow : DetailRow
    {
        private readonly CutsceneActionProperty _property;
        private readonly ColorEditor _editor;
        private readonly MGTextBlock _valueText;

        public ColorRow(CutsceneDetailsPanel owner, CutsceneActionData action, CutsceneActionProperty property)
            : base(owner, action, GetLabel(property.Key))
        {
            _property = property;
            _editor = new ColorEditor(owner._window, (Color?)property.GetValue(action));
            _editor.ValueChanged += OnValueChanged;
            _valueText = new MGTextBlock(owner._window, string.Empty, null, null, false)
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Opacity = EditorThemePalette.SecondaryTextOpacity,
            };

            var line = new MGStackPanel(owner._window, Orientation.Horizontal)
            {
                VerticalAlignment = VerticalAlignment.Center,
            };
            line.TryAddChild(_editor);
            line.TryAddChild(_valueText);

            SetField(WithClearButton(property, line, Clear));
            line.HorizontalAlignment = HorizontalAlignment.Stretch;
            ShowValueText();
        }

        public override string Key => _property.Key;

        public override MGElement Field => _editor;

        public ColorEditor Editor => _editor;

        public override void Detach() => _editor.ValueChanged -= OnValueChanged;

        protected override void RefreshField()
        {
            _editor.Value = (Color?)_property.GetValue(Action) ?? Color.White;
            ShowValueText();
        }

        private void ShowValueText()
        {
            object? value = _property.GetValue(Action);
            _valueText.SetText(value == null ? "(not set)" : _property.FormatValue(value));
        }

        private void OnValueChanged(object? sender, Color color)
        {
            if (Owner._isUpdating)
            {
                return;
            }

            Complete(Owner.WriteValue(Action, _property, color));
        }

        public void Clear() => Complete(Owner.WriteValue(Action, _property, null));
    }

    /// <summary>The three channels of a screen fade as one colour (ADR-0136): a validation writes the three bytes as one entry.</summary>
    internal sealed class RgbColorRow : DetailRow
    {
        private readonly CutsceneActionProperty _red;
        private readonly CutsceneActionProperty _green;
        private readonly CutsceneActionProperty _blue;
        private readonly ColorEditor _editor;
        private readonly MGTextBlock _valueText;

        public RgbColorRow(CutsceneDetailsPanel owner, CutsceneActionData action)
            : base(owner, action, "Color")
        {
            _red = CutsceneActionPropertyEditor.FindProperty(action, "r")!;
            _green = CutsceneActionPropertyEditor.FindProperty(action, "g")!;
            _blue = CutsceneActionPropertyEditor.FindProperty(action, "b")!;

            _editor = new ColorEditor(owner._window, GetColor());
            _editor.ValueChanged += OnValueChanged;
            _valueText = new MGTextBlock(owner._window, string.Empty, null, null, false)
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Opacity = EditorThemePalette.SecondaryTextOpacity,
            };

            var line = new MGStackPanel(owner._window, Orientation.Horizontal)
            {
                VerticalAlignment = VerticalAlignment.Center,
            };
            line.TryAddChild(_editor);
            line.TryAddChild(_valueText);
            SetField(line);
            line.HorizontalAlignment = HorizontalAlignment.Stretch;
            ShowValueText();
        }

        public override string Key => "r,g,b";

        public override MGElement Field => _editor;

        public ColorEditor Editor => _editor;

        public override void Detach() => _editor.ValueChanged -= OnValueChanged;

        protected override void RefreshField()
        {
            _editor.Value = GetColor();
            ShowValueText();
        }

        private Color GetColor() => new((byte)_red.GetValue(Action)!, (byte)_green.GetValue(Action)!, (byte)_blue.GetValue(Action)!, (byte)255);

        private void ShowValueText()
        {
            var color = GetColor();
            _valueText.SetText(string.Create(CultureInfo.InvariantCulture, $"{color.R}, {color.G}, {color.B}"));
        }

        private void OnValueChanged(object? sender, Color color)
        {
            if (Owner._isUpdating || Owner._document == null)
            {
                return;
            }

            var changes = new[]
            {
                new CutscenePropertyChange(_red, color.R),
                new CutscenePropertyChange(_green, color.G),
                new CutscenePropertyChange(_blue, color.B),
            };
            Complete(Owner._document.SetProperties(Action, "Set the fade color", changes));
        }
    }

    /// <summary>A vector: three number fields that validate the whole vector.</summary>
    internal sealed class VectorRow : DetailRow
    {
        private static readonly string[] AxisNames = { "X", "Y", "Z" };

        private readonly CutsceneActionProperty _property;
        private readonly CutsceneNumberField[] _fields = new CutsceneNumberField[3];

        public VectorRow(CutsceneDetailsPanel owner, CutsceneActionData action, CutsceneActionProperty property)
            : base(owner, action, GetLabel(property.Key))
        {
            _property = property;

            var grid = new MGGrid(owner._window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ColumnSpacing = 4,
            };
            grid.AddRow(GridLength.Auto);
            for (int axis = 0; axis < 3; axis++)
            {
                grid.AddColumn(GridLength.CreatePixelLength(14));
                grid.AddColumn(GridLength.CreateWeightedLength(1));
            }

            for (int axis = 0; axis < 3; axis++)
            {
                var field = new CutsceneNumberField(owner._window, false, null, null, false, 1, showButtons: false);
                field.CommitRequested += OnCommitRequested;
                _fields[axis] = field;
                grid.TryAddChild(0, axis * 2, new MGTextBlock(owner._window, AxisNames[axis], null, null, false)
                {
                    Opacity = EditorThemePalette.SectionLabelOpacity,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                grid.TryAddChild(0, axis * 2 + 1, field.Element);
            }

            FillFields();
            SetField(grid);
        }

        public override string Key => _property.Key;

        public override MGElement Field => _fields[0].Element;

        public IReadOnlyList<CutsceneNumberField> Fields => _fields;

        public override void Flush()
        {
            for (int axis = 0; axis < _fields.Length; axis++)
            {
                _fields[axis].Flush();
            }
        }

        public override void Detach()
        {
            for (int axis = 0; axis < _fields.Length; axis++)
            {
                _fields[axis].CommitRequested -= OnCommitRequested;
                _fields[axis].Detach();
            }
        }

        protected override void RefreshField() => FillFields();

        private void FillFields()
        {
            var vector = (Vector3)_property.GetValue(Action)!;
            _fields[0].SetText(vector.X.ToString("R", CultureInfo.InvariantCulture));
            _fields[1].SetText(vector.Y.ToString("R", CultureInfo.InvariantCulture));
            _fields[2].SetText(vector.Z.ToString("R", CultureInfo.InvariantCulture));
        }

        private void OnCommitRequested(CutsceneNumberField field, bool isEnter)
        {
            if (Owner._isUpdating)
            {
                return;
            }

            bool pending = false;
            for (int axis = 0; axis < _fields.Length; axis++)
            {
                pending |= _fields[axis].HasPendingText;
            }

            if (!pending && !(isEnter && Owner.HasProblem(Action, _property.Key)))
            {
                return;
            }

            string text = string.Concat(_fields[0].Text, ", ", _fields[1].Text, ", ", _fields[2].Text);
            Complete(Owner.WriteText(Action, _property, text));
        }
    }

    /// <summary>
    /// A reference chosen in a drop-down list: an entity of the preview world, or an asset of the catalogue. The list holds
    /// "(none)", the choices, and the stored value marked "(missing)" when it is not among them: it is kept, never dropped.
    /// </summary>
    internal sealed class ChoiceRow : DetailRow
    {
        private readonly CutsceneActionProperty _property;
        private readonly IReadOnlyList<CutsceneReferenceOption>? _options;
        private readonly List<string>? _entityNames;
        private readonly MGComboBox<string> _combo;
        private readonly List<string> _labels = new();
        private readonly List<string> _values = new();
        private string? _missingValue;

        public ChoiceRow(
            CutsceneDetailsPanel owner,
            CutsceneActionData action,
            CutsceneActionProperty property,
            IReadOnlyList<CutsceneReferenceOption>? options,
            List<string>? entityNames)
            : base(owner, action, GetLabel(property.Key))
        {
            _property = property;
            _options = options;
            _entityNames = entityNames;

            _combo = new MGComboBox<string>(owner._window)
            {
                MinWidth = 100,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _combo.DropdownItemTemplate = item =>
            {
                var button = _combo.CreateDefaultDropdownButton();
                button.SetContent(item);
                return button;
            };
            _combo.SelectedItemTemplate = item => new MGTextBlock(owner._window, item, null, null, false)
            {
                Padding = new Thickness(4, 1, 4, 1),
                VerticalAlignment = VerticalAlignment.Center,
            };

            BuildItems();
            _combo.SelectedItemChanged += OnSelectedItemChanged;
            SetField(_combo);
        }

        public override string Key => _property.Key;

        public override MGElement Field => _combo;

        public MGComboBox<string> Combo => _combo;

        /// <summary>The labels of the list, in order: "(none)", the choices, then the missing value if any.</summary>
        public IReadOnlyList<string> Labels => _labels;

        /// <summary>The text each label writes, in the order of <see cref="Labels"/>.</summary>
        public IReadOnlyList<string> Values => _values;

        public override void Detach() => _combo.SelectedItemChanged -= OnSelectedItemChanged;

        protected override void RefreshField()
        {
            if (!string.Equals(GetMissingValue(), _missingValue, StringComparison.Ordinal))
            {
                BuildItems();
                return;
            }

            _combo.SelectedItem = _labels[GetCurrentIndex()];
        }

        private string NoneValue => _property.Kind == CutsceneActionPropertyKind.Guid ? Guid.Empty.ToString() : string.Empty;

        private string GetCurrentText()
        {
            string text = _property.GetText(Action);
            return _property.Kind == CutsceneActionPropertyKind.Guid && Guid.TryParse(text, out Guid id) && id == Guid.Empty
                ? string.Empty
                : text;
        }

        /// <summary>The stored value when it designates nothing of the list, otherwise null.</summary>
        private string? GetMissingValue()
        {
            string text = GetCurrentText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (_options != null)
            {
                return CutsceneReferenceOptions.Find(_property.Semantic, _options, text) == null ? text : null;
            }

            return _entityNames!.Contains(text) ? null : text;
        }

        private void BuildItems()
        {
            _labels.Clear();
            _values.Clear();
            _labels.Add(NoneItem);
            _values.Add(NoneValue);

            if (_options != null)
            {
                for (int index = 0; index < _options.Count; index++)
                {
                    _labels.Add(_options[index].Label);
                    _values.Add(_options[index].Value);
                }
            }
            else
            {
                for (int index = 0; index < _entityNames!.Count; index++)
                {
                    _labels.Add(_entityNames[index]);
                    _values.Add(_entityNames[index]);
                }
            }

            _missingValue = GetMissingValue();
            if (_missingValue != null)
            {
                _labels.Add(_missingValue + " (missing)");
                _values.Add(_missingValue);
            }

            bool wasUpdating = Owner._isUpdating;
            Owner._isUpdating = true;
            try
            {
                _combo.SetItemsSource(_labels.ToArray());
                _combo.SelectedItem = _labels[GetCurrentIndex()];
            }
            finally
            {
                Owner._isUpdating = wasUpdating;
            }
        }

        private int GetCurrentIndex()
        {
            if (_missingValue != null)
            {
                return _labels.Count - 1;
            }

            string text = GetCurrentText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            if (_options != null)
            {
                var found = CutsceneReferenceOptions.Find(_property.Semantic, _options, text);
                for (int index = 0; found != null && index < _options.Count; index++)
                {
                    if (ReferenceEquals(_options[index], found))
                    {
                        return index + 1;
                    }
                }

                return 0;
            }

            int position = _entityNames!.IndexOf(text);
            return position < 0 ? 0 : position + 1;
        }

        private void OnSelectedItemChanged(object? sender, MGUI.Shared.Helpers.EventArgs<string> args)
        {
            if (Owner._isUpdating || args.NewValue == null)
            {
                return;
            }

            int index = _labels.IndexOf(args.NewValue);
            if (index < 0)
            {
                return;
            }

            Complete(Owner.WriteText(Action, _property, _values[index]));
        }
    }

    /// <summary>Free text, a list of names, an identifier: a text box validated by Enter or by the loss of the focus.</summary>
    internal sealed class TextRow : DetailRow
    {
        private readonly CutsceneActionProperty _property;
        private readonly MGTextBox _textBox;
        private readonly CutsceneTextCommitBinding _binding;
        private string _committedText;
        private bool _isFilling;

        public TextRow(CutsceneDetailsPanel owner, CutsceneActionData action, CutsceneActionProperty property)
            : base(owner, action, GetLabel(property.Key))
        {
            _property = property;
            _textBox = new MGTextBox(owner._window, CharacterLimit: 256)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                HasStableTextFootprint = true,
                AcceptsReturn = false,
                AcceptsTab = false,
                MinWidth = 60,
            };

            _committedText = property.GetText(action);
            _isFilling = true;
            _textBox.SetText(_committedText);
            _isFilling = false;

            _binding = new CutsceneTextCommitBinding(owner._window, _textBox, OnCommit);
            SetField(_textBox);
        }

        public override string Key => _property.Key;

        public override MGElement Field => _textBox;

        public MGTextBox TextBox => _textBox;

        public bool HasPendingText => !string.Equals(_textBox.Text ?? string.Empty, _committedText, StringComparison.Ordinal);

        public override void Flush()
        {
            if (HasPendingText)
            {
                OnCommit(false);
            }
        }

        public override void Detach() => _binding.Detach();

        protected override void RefreshField()
        {
            _committedText = _property.GetText(Action);
            if (string.Equals(_textBox.Text ?? string.Empty, _committedText, StringComparison.Ordinal))
            {
                return;
            }

            _isFilling = true;
            _textBox.SetText(_committedText);
            _isFilling = false;
        }

        private void OnCommit(bool isEnter)
        {
            if (_isFilling || Owner._isUpdating)
            {
                return;
            }

            // Enter writes a field with a problem even when its text is unchanged: that is how the problem is cleared.
            if (!HasPendingText && !(isEnter && Owner.HasProblem(Action, _property.Key)))
            {
                return;
            }

            Complete(Owner.WriteText(Action, _property, _textBox.Text ?? string.Empty));
        }
    }
}
