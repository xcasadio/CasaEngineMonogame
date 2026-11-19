using System;
using System.Collections.Generic;
using CasaEngine.Editor.Controls.Cutscenes;
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
/// The Hierarchy view of a cutscene document (ADR-0136): the action tree with one summary line per action, and a bar that
/// adds, duplicates and deletes actions. It never edits the asset itself: every change goes through the operations of the
/// document, and the tree is rebuilt when the document raises <c>DocumentChanged</c>, never per frame.
/// </summary>
internal sealed partial class CutsceneOutlinerPanel
{
    private readonly MGWindow _window;
    private readonly Dictionary<MGTreeViewItem, CutsceneActionData> _itemToAction = new();
    private readonly Dictionary<CutsceneActionData, MGTreeViewItem> _actionToItem = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<CutsceneActionData> _expandedActions = new(ReferenceEqualityComparer.Instance);
    // The containers of the tree as last built, and of the tree being built: a container absent from the first one appears.
    private HashSet<CutsceneActionData> _previousContainers = new(ReferenceEqualityComparer.Instance);
    private HashSet<CutsceneActionData> _currentContainers = new(ReferenceEqualityComparer.Instance);
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
    private bool _scrollToSelectionAfterLayout;
    private bool _expandAllOnNextBuild;
    private bool _itemRightClicked;
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
        _treeView.OnLayoutUpdated += OnTreeLayoutUpdated;
        _treeView.MouseHandler.RMBReleasedInside += OnTreeRightClick;
        AttachDrag(_treeView);

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
        _previousContainers.Clear();
        _expandAllOnNextBuild = true;

        if (_document != null)
        {
            _document.DocumentChanged += OnDocumentChanged;
            _document.SelectionChanged += OnDocumentSelectionChanged;
        }

        Rebuild();
    }

    /// <summary>Adds the action of the type chosen in the list, by the rule of the insertion of ADR-0136.</summary>
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

    // A tree that was just rebuilt has no bounds yet: the selected item is brought into view once it is laid out.
    private void OnTreeLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_scrollToSelectionAfterLayout || _treeView == null)
        {
            return;
        }

        var selected = _treeView.SelectedItem;
        if (selected == null)
        {
            _scrollToSelectionAfterLayout = false;
            return;
        }

        if (EnsureItemVisible(selected))
        {
            _scrollToSelectionAfterLayout = false;
        }
    }

    // MGTreeView.ScrollIntoView (called by SelectItem) and MGScrollViewer.EnsureElementVisible both fail on this tree: the
    // first compares the layout bounds with a viewport that adds the offset, the second adds the bounds of every parent
    // again, so the nested rows end up scrolled to the bottom. The layout bounds of a header do not move when the tree
    // scrolls (the drag code measures them the same way), so the row is shown where its bounds minus the offset the scroll is
    // heading to put it, and that is compared with the visible area. The offset moves only when the row is not fully
    // visible. Returns false while the row has no layout yet.
    private bool EnsureItemVisible(MGTreeViewItem item)
    {
        var scrollViewer = _treeView?.ScrollViewer;
        var header = item.HeaderContent;
        if (scrollViewer == null || header == null || header.LayoutBounds.Height <= 0
            || scrollViewer.ContentViewport.Height <= 0)
        {
            return false;
        }

        var row = header.LayoutBounds;
        var viewport = scrollViewer.ContentViewport;
        float pending = scrollViewer.PendingVerticalOffset;
        // Where the row will be shown once a running scroll animation has reached its target.
        float rowTop = row.Top - pending;
        float rowBottom = row.Bottom - pending;

        float target = pending;
        if (rowTop < viewport.Top)
        {
            target = pending - (viewport.Top - rowTop);
        }
        else if (rowBottom > viewport.Bottom)
        {
            target = pending + (rowBottom - viewport.Bottom);
        }

        target = Math.Clamp(target, 0f, scrollViewer.MaxVerticalOffset);
        if (Math.Abs(target - pending) > 0.5f)
        {
            scrollViewer.ScrollTo(null, target, scrollViewer.ScrollAnimationDuration, scrollViewer.ScrollAnimationEasing);
        }

        return true;
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
            _itemRightClicked = true;
        }
    }

    private void OnTreeRightClick(object? sender, BaseMouseReleasedEventArgs e)
    {
        bool onItem = _itemRightClicked;
        _itemRightClicked = false;
        if (_treeView == null || _document?.Session?.Asset.RootAction == null)
        {
            return;
        }

        // A click in the empty part of the tree aims at no action: the menu then only adds and pastes at the end of the root.
        if (!onItem)
        {
            _document.SelectAction(null);
        }

        var menu = CutsceneActionMenu.Create(_window, _document, ShowRefusal);
        if (menu != null)
        {
            _treeView.GetDesktop().TryOpenContextMenu(menu, e.Position);
        }
    }

    private void Rebuild()
    {
        if (_treeView == null)
        {
            return;
        }

        // A rebuilt tree has new rows: a drag in progress would aim at rows that no longer exist.
        CancelDrag();
        _visibleRowsDirty = true;
        _itemToAction.Clear();
        _actionToItem.Clear();
        _treeView.ClearItems();
        _actionCount = 0;
        _currentContainers.Clear();

        var root = _document?.Session?.Asset.RootAction;
        if (root != null)
        {
            var rootItem = BuildItem(root);
            rootItem.IsExpanded = true;
            _expandedActions.Add(root);
            _treeView.AddItem(rootItem);
        }

        // The containers of this build are the reference of the next one.
        (_previousContainers, _currentContainers) = (_currentContainers, _previousContainers);
        _expandAllOnNextBuild = false;
        ApplySelection(_document?.SelectedAction);
        UpdateStatus();
        UpdateButtons();
    }

    private MGTreeViewItem BuildItem(CutsceneActionData action)
    {
        var children = CutsceneActionCatalog.GetChildren(action);
        if (CutsceneActionCatalog.CanHaveChildren(action))
        {
            // The first build of a document expands the containers that hold something; after that, a container that was not in
            // the previous tree (a wrap, a paste, an undo that brings it back) is shown expanded.
            if (_expandAllOnNextBuild ? children.Count > 0 : !_previousContainers.Contains(action))
            {
                _expandedActions.Add(action);
            }

            _currentContainers.Add(action);
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
                bool expanded = false;
                for (var parent = item.ParentItem; parent != null; parent = parent.ParentItem)
                {
                    expanded |= !parent.IsExpanded;
                    parent.IsExpanded = true;
                    if (_itemToAction.TryGetValue(parent, out var parentAction))
                    {
                        _expandedActions.Add(parentAction);
                    }
                }

                // SelectItem scrolls with the faulty MGTreeView.ScrollIntoView: the offset it sets is undone below.
                var scrollViewer = _treeView.ScrollViewer;
                float offsetBefore = scrollViewer.PendingVerticalOffset;
                _treeView.SelectItem(item);
                if (Math.Abs(scrollViewer.PendingVerticalOffset - offsetBefore) > 0.5f)
                {
                    scrollViewer.ScrollTo(null, offsetBefore, TimeSpan.Zero, null);
                }

                // A parent that was just expanded changes the layout: the bounds read now would be stale.
                _scrollToSelectionAfterLayout = expanded || !EnsureItemVisible(item);
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

    internal void ShowRefusal(string? message)
    {
        if (message != null)
        {
            _statusText?.SetText(message);
        }
    }
}
