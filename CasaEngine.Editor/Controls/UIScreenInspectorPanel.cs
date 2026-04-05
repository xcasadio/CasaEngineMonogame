using System;
using System.Collections.Generic;
using CasaEngine.EditorServices.ScreenEditor.Commands;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Inspector;
using CasaEngine.EditorServices.ScreenEditor.Selection;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// Dockable panel that shows and edits the properties of the currently
/// selected <see cref="UIScreenNode"/> via the shared
/// <see cref="UIScreenSelectionService"/>.
/// </summary>
public sealed class UIScreenInspectorPanel
{
    private readonly MGWindow _window;
    private readonly UIScreenSelectionService _selection;
    private readonly UIPropertyRegistry _registry;
    private UICommandStack? _commandStack;

    private MGDockPanel? _root;
    private MGStackPanel? _propertiesStack;
    private MGTextBlock? _headerText;
    private MGTextBlock? _statusText;
    private MGTextBox? _nameEditor;
    private bool _hasDesktopFocusSubscription;

    private UIScreenDocument? _document;
    private bool _refreshPending;
    private bool _suppressEditorEvents;

    private MGTextBox? _activeTextTransactionEditor;
    private UIScreenNode? _activeTextTransactionNode;
    private string? _activeTextTransactionPropertyName;
    private string? _activeTextTransactionInitialValue;
    private string? _activeTextTransactionDescription;

    // R-05: track last node to skip full rebuild on same-node re-selection
    private DocumentNodeId? _lastRenderedNodeId;
    private string? _lastRenderedControlType;

    private readonly List<(MGTextBox Editor, UIPropertyDescriptor Descriptor, MGTextBlock ErrorLabel)> _editors = new();

    // ─────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Fired when a property is successfully changed in the document.</summary>
    public event Action<UIScreenDocument>? DocumentModified;

    /// <summary>
    /// Fired when a single property is changed.
    /// Args: document, nodeId, propertyName, newSerializedValue.
    /// Use this for the incremental preview update path (R-01).
    /// </summary>
    public event Action<UIScreenDocument, DocumentNodeId, string, string?>? PropertyModified;

    // ─────────────────────────────────────────────────────────────────────
    //  Constructor
    // ─────────────────────────────────────────────────────────────────────

    public UIScreenInspectorPanel(MGWindow window, UIScreenSelectionService selection)
        : this(window, selection, UIPropertyRegistry.Default)
    {
    }

    public UIScreenInspectorPanel(MGWindow window, UIScreenSelectionService selection, UIPropertyRegistry registry)
    {
        _window = window;
        _selection = selection;
        _registry = registry;
        _selection.SelectionChanged += OnSelectionChanged;
    }

    /// <summary>
    /// Attaches a command stack so property edits are undoable.
    /// Must be set before <see cref="CreateContent"/> is called, or before
    /// any edits are made.
    /// </summary>
    public void SetCommandStack(UICommandStack commandStack) => _commandStack = commandStack;

    // ─────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────

    public void SetDocument(UIScreenDocument? document)
    {
        FinalizeActiveTextTransaction();
        _document = document;
        ScheduleRefreshInspector();
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _headerText = new MGTextBlock(_window, "[b]Inspector[/b]")
        {
            Margin = new Thickness(8, 6, 8, 4),
        };

        _statusText = new MGTextBlock(_window, "No node selected.")
        {
            Margin = new Thickness(8, 4, 8, 4),
            Opacity = 0.75f,
            WrapText = true,
        };

        _propertiesStack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 2,
            Margin = new Thickness(4),
        };

        var scrollViewer = new MGScrollViewer(_window);
        scrollViewer.SetContent(_propertiesStack);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(_headerText, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Top);
        _root.TryAddChild(scrollViewer, Dock.Top);

        if (!_hasDesktopFocusSubscription && _window.Desktop != null)
        {
            _window.Desktop.FocusedKeyboardHandlerChanged += OnFocusedKeyboardHandlerChanged;
            _hasDesktopFocusSubscription = true;
        }

        if (_refreshPending || _document != null)
        {
            _refreshPending = false;
            RefreshInspector();
        }

        return _root;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Selection → refresh
    // ─────────────────────────────────────────────────────────────────────

    private void OnSelectionChanged(DocumentNodeId? nodeId)
    {
        FinalizeActiveTextTransaction();
        ScheduleRefreshInspector();
    }

    private void ScheduleRefreshInspector()
    {
        if (_refreshPending)
        {
            return;
        }

        _refreshPending = true;

        if (_root == null)
        {
            return;
        }

        _root.InvokeLater(() =>
        {
            _refreshPending = false;
            RefreshInspector();
        }, 1, MGElement.InvokeLaterPriority.OnEndUpdate);
    }

    private void RefreshInspector()
    {
        if (_propertiesStack == null)
        {
            return;
        }

        if (_document == null || !_selection.SelectedNodeId.HasValue)
        {
            if (_statusText != null)
            {
                _statusText.Text = _document == null ? "No screen loaded." : "No node selected.";
            }

            // Clear cached tracking when there's nothing selected
            _lastRenderedNodeId = null;
            _lastRenderedControlType = null;

            _propertiesStack.TryRemoveAll();
            _editors.Clear();
            return;
        }

        var nodeId = _selection.SelectedNodeId.Value;

        // R-05: If the same node is re-selected, just update the field values in-place.
        if (nodeId == _lastRenderedNodeId)
        {
            var node = _document.FindNode(nodeId);
            if (node != null)
            {
                UpdateEditorValues(node);
                return;
            }
        }

        // Different node (or first load) — full rebuild
        FullRebuildInspector(nodeId);
    }

    /// <summary>Updates only the text-box values of existing editor rows — no MGUI element recreation.</summary>
    private void UpdateEditorValues(UIScreenNode node)
    {
        if (_nameEditor != null)
        {
            string currentName = node.Name ?? string.Empty;
            if (!string.Equals(_nameEditor.Text, currentName, StringComparison.Ordinal))
            {
                _suppressEditorEvents = true;
                try
                {
                    _nameEditor.Text = currentName;
                }
                finally
                {
                    _suppressEditorEvents = false;
                }
            }
        }

        foreach (var (editor, desc, _) in _editors)
        {
            var currentValue = node.Properties.TryGetValue(desc.Name, out var prop)
                ? prop.SerializedValue ?? string.Empty
                : desc.DefaultSerializedValue ?? string.Empty;

            // Suppress the TextChanged event while updating to avoid a feedback loop
            if (!string.Equals(editor.Text, currentValue, StringComparison.Ordinal))
            {
                _suppressEditorEvents = true;
                try
                {
                    editor.Text = currentValue;
                }
                finally
                {
                    _suppressEditorEvents = false;
                }
            }
        }
    }

    private void FullRebuildInspector(DocumentNodeId nodeId)
    {
        _propertiesStack!.TryRemoveAll();
        _editors.Clear();
        _lastRenderedNodeId = null;
        _lastRenderedControlType = null;

        var node = _document!.FindNode(nodeId);
        if (node == null)
        {
            if (_statusText != null)
            {
                _statusText.Text = "Node not found.";
            }

            return;
        }

        if (_statusText != null)
        {
            _statusText.Text = string.Empty;
        }

        _lastRenderedNodeId = nodeId;
        _lastRenderedControlType = node.ControlType;

        // Name row (special — not a UIScreenPropertyValue but directly on node)
        _propertiesStack.TryAddChild(BuildSectionHeader("General"));
        _propertiesStack.TryAddChild(BuildNameRow(node));

        // Grouped property rows
        string? lastCategory = null;
        foreach (var desc in _registry.GetDescriptors(node.ControlType))
        {
            if (!string.Equals(desc.Category, lastCategory, StringComparison.Ordinal))
            {
                _propertiesStack.TryAddChild(BuildSectionHeader(desc.Category));
                lastCategory = desc.Category;
            }

            _propertiesStack.TryAddChild(BuildPropertyRow(node, desc));
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Row builders
    // ─────────────────────────────────────────────────────────────────────

    private MGElement BuildSectionHeader(string category)
    {
        return new MGTextBlock(_window, $"[b]{EscapeMarkup(category)}[/b]")
        {
            Margin = new Thickness(4, 6, 4, 2),
            Opacity = 0.8f,
        };
    }

    private MGElement BuildNameRow(UIScreenNode node)
    {
        var row = new MGDockPanel(_window)
        {
            Margin = new Thickness(2, 1, 2, 1),
        };
        row.TryAddChild(new MGTextBlock(_window, "Name")
        {
            PreferredWidth = 90,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 6, 0),
        }, Dock.Left);

        var errorLabel = BuildErrorLabel();
        var textBox = new MGTextBox(_window)
        {
            Text = node.Name ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _nameEditor = textBox;
        textBox.TextChanged += (_, args) =>
        {
            if (_suppressEditorEvents)
            {
                return;
            }

            var newName = args.NewValue?.Trim();
            if (string.Equals(node.Name, newName, StringComparison.Ordinal))
            {
                return;
            }

            ApplyNodeNameChange(node, textBox, string.IsNullOrEmpty(newName) ? null : newName);
            errorLabel.Text = string.Empty;
        };

        var col = new MGStackPanel(_window, Orientation.Vertical) { HorizontalAlignment = HorizontalAlignment.Stretch };
        col.TryAddChild(textBox);
        col.TryAddChild(errorLabel);
        row.TryAddChild(col, Dock.Left);

        return row;
    }

    private MGElement BuildPropertyRow(UIScreenNode node, UIPropertyDescriptor desc)
    {
        var row = new MGDockPanel(_window)
        {
            Margin = new Thickness(2, 1, 2, 1),
        };
        row.TryAddChild(new MGTextBlock(_window, EscapeMarkup(desc.DisplayName))
        {
            PreferredWidth = 90,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 6, 0),
            Opacity = desc.IsEditable ? 1f : 0.5f,
        }, Dock.Left);

        var currentValue = node.Properties.TryGetValue(desc.Name, out var prop)
            ? prop.SerializedValue ?? string.Empty
            : desc.DefaultSerializedValue ?? string.Empty;

        var errorLabel = BuildErrorLabel();

        if (!desc.IsEditable)
        {
            var readOnly = new MGTextBlock(_window, EscapeMarkup(currentValue))
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Opacity = 0.5f,
            };
            row.TryAddChild(readOnly, Dock.Left);
            return row;
        }

        // Boolean → checkbox
        if (desc.ValueType == typeof(bool) || desc.ValueType == typeof(bool?))
        {
            var checkBox = new MGCheckBox(_window);
            checkBox.IsChecked = string.Equals(currentValue, "True", StringComparison.OrdinalIgnoreCase);
            checkBox.OnCheckStateChanged += (_, args) =>
            {
                var serialized = args.NewValue == true ? "True" : "False";
                if (_commandStack != null)
                {
                    _commandStack.Execute(new SetPropertyCommand(node, desc.Name, serialized));
                }
                else
                {
                    node.SetProperty(desc.Name, serialized);
                }
                errorLabel.Text = string.Empty;
                if (_document != null)
                {
                    PropertyModified?.Invoke(_document, node.Id, desc.Name, serialized);
                }
            };
            row.TryAddChild(checkBox, Dock.Left);
            return row;
        }

        // All other types → text box with validation
        var editor = new MGTextBox(_window)
        {
            Text = currentValue,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        editor.TextChanged += (_, args) =>
        {
            if (_suppressEditorEvents)
            {
                return;
            }

            var raw = args.NewValue ?? string.Empty;
            var error = Validate(raw, desc.ValueType);
            errorLabel.Text = error ?? string.Empty;

            if (error == null)
            {
                var serialized = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
                ApplyPropertyChange(node, desc, editor, serialized);
                errorLabel.Text = string.Empty;
            }
        };

        _editors.Add((editor, desc, errorLabel));

        var col = new MGStackPanel(_window, Orientation.Vertical) { HorizontalAlignment = HorizontalAlignment.Stretch };
        col.TryAddChild(editor);
        col.TryAddChild(errorLabel);
        row.TryAddChild(col, Dock.Left);

        return row;
    }

    private MGTextBlock BuildErrorLabel()
    {
        return new MGTextBlock(_window, string.Empty)
        {
            Foreground = new MGUI.Core.UI.VisualStateSetting<Microsoft.Xna.Framework.Color?>(Microsoft.Xna.Framework.Color.OrangeRed, Microsoft.Xna.Framework.Color.OrangeRed, Microsoft.Xna.Framework.Color.OrangeRed),
            FontSize = 8,
            WrapText = true,
        };
    }

    private void ApplyNodeNameChange(UIScreenNode node, MGTextBox editor, string? newName)
    {
        if (_commandStack != null)
        {
            EnsureTextTransaction(editor, node, propertyName: null, node.Name, "Rename UI node");
            _commandStack.Execute(new RenameNodeCommand(node, newName));
            return;
        }

        node.Name = newName;
        if (_document != null)
        {
            DocumentModified?.Invoke(_document);
        }
    }

    private void ApplyPropertyChange(UIScreenNode node, UIPropertyDescriptor descriptor, MGTextBox editor, string? serializedValue)
    {
        string? currentValue = node.Properties.TryGetValue(descriptor.Name, out var property)
            ? property.SerializedValue
            : null;
        if (string.Equals(currentValue, serializedValue, StringComparison.Ordinal))
        {
            return;
        }

        if (_commandStack != null)
        {
            EnsureTextTransaction(editor, node, descriptor.Name, currentValue, $"Edit {descriptor.DisplayName}");
            _commandStack.Execute(new SetPropertyCommand(node, descriptor.Name, serializedValue));
        }
        else
        {
            node.SetProperty(descriptor.Name, serializedValue);
        }

        if (_document != null)
        {
            PropertyModified?.Invoke(_document, node.Id, descriptor.Name, serializedValue);
        }
    }

    private void EnsureTextTransaction(MGTextBox editor, UIScreenNode node, string? propertyName, string? initialValue, string description)
    {
        if (_commandStack == null)
        {
            return;
        }

        if (ReferenceEquals(_activeTextTransactionEditor, editor))
        {
            return;
        }

        FinalizeActiveTextTransaction();
        _commandStack.BeginTransaction(description);
        _activeTextTransactionEditor = editor;
        _activeTextTransactionNode = node;
        _activeTextTransactionPropertyName = propertyName;
        _activeTextTransactionInitialValue = initialValue;
        _activeTextTransactionDescription = description;
    }

    private void FinalizeActiveTextTransaction()
    {
        if (_commandStack == null || _activeTextTransactionEditor == null)
        {
            ClearTextTransaction();
            return;
        }

        if (!_commandStack.IsTransactionOpen)
        {
            ClearTextTransaction();
            return;
        }

        string? currentValue = GetCurrentTransactionValue();
        bool hasChanged = !string.Equals(currentValue, _activeTextTransactionInitialValue, StringComparison.Ordinal);

        if (hasChanged)
        {
            _commandStack.CommitTransaction(_activeTextTransactionDescription);
            if (_document != null && _activeTextTransactionPropertyName == null)
            {
                DocumentModified?.Invoke(_document);
            }
        }
        else
        {
            _commandStack.CancelTransaction();
            if (_document != null && _activeTextTransactionPropertyName != null && _activeTextTransactionNode != null)
            {
                PropertyModified?.Invoke(_document, _activeTextTransactionNode.Id, _activeTextTransactionPropertyName, _activeTextTransactionInitialValue);
            }
        }

        ClearTextTransaction();
    }

    private string? GetCurrentTransactionValue()
    {
        if (_activeTextTransactionNode == null)
        {
            return null;
        }

        if (_activeTextTransactionPropertyName == null)
        {
            return _activeTextTransactionNode.Name;
        }

        return _activeTextTransactionNode.Properties.TryGetValue(_activeTextTransactionPropertyName, out var property)
            ? property.SerializedValue
            : null;
    }

    private void ClearTextTransaction()
    {
        _activeTextTransactionEditor = null;
        _activeTextTransactionNode = null;
        _activeTextTransactionPropertyName = null;
        _activeTextTransactionInitialValue = null;
        _activeTextTransactionDescription = null;
    }

    private void OnFocusedKeyboardHandlerChanged(object? sender, MGUI.Shared.Helpers.EventArgs<MGElement> e)
    {
        if (_activeTextTransactionEditor != null
            && ReferenceEquals(e.PreviousValue, _activeTextTransactionEditor)
            && !ReferenceEquals(e.NewValue, _activeTextTransactionEditor))
        {
            FinalizeActiveTextTransaction();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Validation
    // ─────────────────────────────────────────────────────────────────────

    private static string? Validate(string value, Type type)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            // Empty is always valid (means "use default")
            return null;
        }

        var trimmed = value.Trim();

        if (type == typeof(int) || type == typeof(int?))
        {
            return int.TryParse(trimmed, out _) ? null : "Must be an integer.";
        }

        if (type == typeof(float) || type == typeof(float?))
        {
            return float.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _)
                    ? null
                    : "Must be a number.";
        }

        if (type == typeof(bool) || type == typeof(bool?))
        {
            return string.Equals(trimmed, "True", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "False", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : "Must be True or False.";
        }

        return null;
    }

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}
