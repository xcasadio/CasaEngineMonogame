using System;
using System.Collections.Generic;
using CasaEngine.Editor.Styling;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input.Mouse;
using MonoGame.Extended;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// The Hierarchy view of a cutscene document (ADR-0097): the action tree with one summary line per action, and a bar that
/// adds, duplicates and deletes actions. It never edits the asset itself: every change goes through the operations of the
/// document, and the tree is rebuilt when the document raises <c>DocumentChanged</c>, never per frame.
/// </summary>
internal sealed class CutsceneOutlinerPanel
{
    private readonly MGWindow _window;
    private readonly Dictionary<MGTreeViewItem, CutsceneActionData> _itemToAction = new();
    private readonly Dictionary<CutsceneActionData, MGTreeViewItem> _actionToItem = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<CutsceneActionData> _expandedActions = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, string> _displayNameToType = new(StringComparer.Ordinal);

    private MGDockPanel? _root;
    private MGTreeView? _treeView;
    private MGComboBox<string>? _typeCombo;
    private MGButton? _addButton;
    private MGButton? _duplicateButton;
    private MGButton? _deleteButton;
    private MGTextBlock? _statusText;
    private CutsceneAssetInspectorPanel? _document;
    private bool _suppressSelectionChanged;
    private bool _expandAllOnNextBuild;
    private int _actionCount;

    public CutsceneOutlinerPanel(MGWindow window)
    {
        _window = window;
    }

    /// <summary>The document the tree shows, or null.</summary>
    internal CutsceneAssetInspectorPanel? Document => _document;

    internal MGTreeView? TreeView => _treeView;

    internal MGButton? AddButton => _addButton;

    internal MGButton? DuplicateButton => _duplicateButton;

    internal MGButton? DeleteButton => _deleteButton;

    internal string StatusText => _statusText?.Text ?? string.Empty;

    /// <summary>The tree item of an action, or null when the action is not shown.</summary>
    internal MGTreeViewItem? GetItem(CutsceneActionData action)
        => _actionToItem.TryGetValue(action, out var item) ? item : null;

    /// <summary>Selects a type of the add list by its catalogue name; false when the catalogue has no such type.</summary>
    internal bool SelectType(string type)
    {
        if (_typeCombo == null)
        {
            return false;
        }

        foreach (var kind in CutsceneActionCatalog.Kinds)
        {
            if (kind.Type == type)
            {
                _typeCombo.SelectedItem = kind.DisplayName;
                return true;
            }
        }

        return false;
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _treeView = new MGTreeView(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _treeView.SelectionChanged += OnTreeSelectionChanged;
        _treeView.ItemExpanded += OnTreeItemExpanded;
        _treeView.ItemCollapsed += OnTreeItemCollapsed;
        _treeView.ItemRightClicked += OnTreeItemRightClicked;
        _treeView.MouseHandler.RMBReleasedInside += OnTreeRightClick;

        _statusText = new MGTextBlock(_window, string.Empty)
        {
            Margin = new Thickness(8, 4, 8, 6),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = true,
        };

        _root = new MGDockPanel(_window);
        _root.TryAddChild(BuildToolbar(), Dock.Top);
        _root.TryAddChild(_statusText, Dock.Bottom);
        _root.TryAddChild(_treeView, Dock.Top);

        Rebuild();
        return _root;
    }

    /// <summary>
    /// Shows the tree of a document, or nothing with null. The previous document is released: the panel keeps no
    /// subscription on it.
    /// </summary>
    public void SetEditorPanel(CutsceneAssetInspectorPanel? document)
    {
        if (ReferenceEquals(_document, document))
        {
            return;
        }

        if (_document != null)
        {
            _document.DocumentChanged -= OnDocumentChanged;
            _document.SelectionChanged -= OnDocumentSelectionChanged;
        }

        _document = document;
        _expandedActions.Clear();
        _expandAllOnNextBuild = true;

        if (_document != null)
        {
            _document.DocumentChanged += OnDocumentChanged;
            _document.SelectionChanged += OnDocumentSelectionChanged;
        }

        Rebuild();
    }

    /// <summary>Adds the action of the type chosen in the list, by the rule of the insertion of ADR-0097.</summary>
    internal void AddSelectedType()
    {
        if (_document == null || _typeCombo?.SelectedItem == null
            || !_displayNameToType.TryGetValue(_typeCombo.SelectedItem, out var type))
        {
            return;
        }

        ShowRefusal(_document.AddAction(type));
    }

    internal void DuplicateSelected()
    {
        if (_document != null)
        {
            ShowRefusal(_document.DuplicateSelectedAction());
        }
    }

    internal void DeleteSelected()
    {
        if (_document != null)
        {
            ShowRefusal(_document.RemoveSelectedAction());
        }
    }

    private MGElement BuildToolbar()
    {
        var toolbar = new MGStackPanel(_window, Orientation.Vertical)
        {
            Margin = new Thickness(6, 6, 6, 4),
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        _typeCombo = new MGComboBox<string>(_window)
        {
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _typeCombo.DropdownItemTemplate = item =>
        {
            var button = _typeCombo.CreateDefaultDropdownButton();
            button.SetContent(item);
            return button;
        };
        _typeCombo.SelectedItemTemplate = item => new MGTextBlock(_window, item)
        {
            Padding = new Thickness(4, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var names = new List<string>(CutsceneActionCatalog.Kinds.Count);
        foreach (var kind in CutsceneActionCatalog.Kinds)
        {
            string name = kind.DisplayName;
            names.Add(name);
            _displayNameToType[name] = kind.Type;
        }

        _typeCombo.SetItemsSource(names);
        _typeCombo.SelectedItem = names[0];

        _addButton = CreateButton("Add", AddSelectedType);

        var addRow = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _addButton.Margin = new Thickness(6, 0, 0, 0);
        addRow.TryAddChild(_addButton, Dock.Right);
        addRow.TryAddChild(_typeCombo, Dock.Left);

        _duplicateButton = CreateButton("Duplicate", DuplicateSelected);
        _deleteButton = CreateButton("Delete", DeleteSelected);
        var actionRow = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
        };
        actionRow.TryAddChild(_duplicateButton);
        actionRow.TryAddChild(_deleteButton);

        toolbar.TryAddChild(addRow);
        toolbar.TryAddChild(actionRow);
        return toolbar;
    }

    private MGButton CreateButton(string label, Action onClick)
    {
        var button = new MGButton(_window, _ => onClick())
        {
            Padding = new Thickness(12, 3, 12, 3),
        };
        button.SetContent(new MGTextBlock(_window, label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return button;
    }

    private void OnDocumentChanged(CutsceneAssetInspectorPanel document)
    {
        Rebuild();
    }

    private void OnDocumentSelectionChanged(CutsceneAssetInspectorPanel document)
    {
        ApplySelection(_document?.SelectedAction);
        UpdateButtons();
    }

    private void OnTreeSelectionChanged(object? sender, MGTreeViewItem item)
    {
        if (_suppressSelectionChanged || _document == null)
        {
            return;
        }

        _document.SelectAction(item != null && _itemToAction.TryGetValue(item, out var action) ? action : null);
    }

    private void OnTreeItemExpanded(object? sender, MGTreeViewItem item)
    {
        if (_itemToAction.TryGetValue(item, out var action))
        {
            _expandedActions.Add(action);
        }
    }

    private void OnTreeItemCollapsed(object? sender, MGTreeViewItem item)
    {
        if (_itemToAction.TryGetValue(item, out var action))
        {
            _expandedActions.Remove(action);
        }
    }

    private void OnTreeItemRightClicked(object? sender, MGTreeViewItem item)
    {
        // The menu then aims at the item under the cursor, as the selected action.
        if (_document != null && _itemToAction.TryGetValue(item, out var action))
        {
            _document.SelectAction(action);
        }
    }

    private void OnTreeRightClick(object? sender, BaseMouseReleasedEventArgs e)
    {
        var selected = _document?.SelectedAction;
        var root = _document?.Session?.Asset.RootAction;
        if (_treeView == null || selected == null || ReferenceEquals(selected, root))
        {
            return;
        }

        var menu = new MGContextMenu(_window, null);
        menu.AddButton("Duplicate", _ => DuplicateSelected());
        menu.AddSeparator();
        menu.AddButton("Delete", _ => DeleteSelected());
        _treeView.GetDesktop().TryOpenContextMenu(menu, e.Position);
    }

    private void Rebuild()
    {
        if (_treeView == null)
        {
            return;
        }

        _itemToAction.Clear();
        _actionToItem.Clear();
        _treeView.ClearItems();
        _actionCount = 0;

        var root = _document?.Session?.Asset.RootAction;
        if (root != null)
        {
            var rootItem = BuildItem(root);
            rootItem.IsExpanded = true;
            _expandedActions.Add(root);
            _treeView.AddItem(rootItem);
        }

        _expandAllOnNextBuild = false;
        ApplySelection(_document?.SelectedAction);
        UpdateStatus();
        UpdateButtons();
    }

    private MGTreeViewItem BuildItem(CutsceneActionData action)
    {
        var children = CutsceneActionCatalog.GetChildren(action);
        if (_expandAllOnNextBuild && children.Count > 0)
        {
            _expandedActions.Add(action);
        }

        var item = new MGTreeViewItem(_window)
        {
            IsExpanded = _expandedActions.Contains(action),
            Header = BuildHeader(action),
        };
        _itemToAction[item] = action;
        _actionToItem[action] = item;
        _actionCount++;

        for (int index = 0; index < children.Count; index++)
        {
            item.AddItem(BuildItem(children[index]));
        }

        return item;
    }

    private MGElement BuildHeader(CutsceneActionData action)
    {
        string summary = CutsceneActionSummary.Describe(action);
        string detail = summary.StartsWith(action.Type, StringComparison.Ordinal)
            ? summary.Substring(action.Type.Length).TrimStart()
            : summary;

        var header = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.TryAddChild(new MGTextBlock(_window, action.Type, null, null, false)
        {
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        });

        if (detail.Length > 0)
        {
            header.TryAddChild(new MGTextBlock(_window, detail, null, null, false)
            {
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = EditorThemePalette.SecondaryTextOpacity,
                IsHitTestVisible = false,
            });
        }

        return header;
    }

    private void ApplySelection(CutsceneActionData? action)
    {
        if (_treeView == null)
        {
            return;
        }

        _suppressSelectionChanged = true;
        try
        {
            if (action != null && _actionToItem.TryGetValue(action, out var item))
            {
                for (var parent = item.ParentItem; parent != null; parent = parent.ParentItem)
                {
                    parent.IsExpanded = true;
                    if (_itemToAction.TryGetValue(parent, out var parentAction))
                    {
                        _expandedActions.Add(parentAction);
                    }
                }

                _treeView.SelectItem(item);
                _treeView.ScrollIntoView(item);
            }
            else
            {
                _treeView.ClearSelection();
            }
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
    }

    private void UpdateButtons()
    {
        if (_addButton == null || _duplicateButton == null || _deleteButton == null)
        {
            return;
        }

        var root = _document?.Session?.Asset.RootAction;
        var selected = _document?.SelectedAction;
        bool hasDocument = root != null;
        bool hasEditableSelection = selected != null && !ReferenceEquals(selected, root);

        _addButton.IsEnabled = hasDocument;
        _duplicateButton.IsEnabled = hasEditableSelection;
        _deleteButton.IsEnabled = hasEditableSelection;
    }

    private void UpdateStatus()
    {
        if (_statusText == null)
        {
            return;
        }

        _statusText.SetText(_document?.Session?.Asset.RootAction == null
            ? "No cutscene loaded."
            : _actionCount == 1 ? "1 action" : $"{_actionCount} actions");
    }

    private void ShowRefusal(string? message)
    {
        if (message != null)
        {
            _statusText?.SetText(message);
        }
    }
}
