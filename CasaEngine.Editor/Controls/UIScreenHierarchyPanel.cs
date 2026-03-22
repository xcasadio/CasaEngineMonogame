using System;
using System.Collections.Generic;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Selection;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// Dockable panel that displays the node tree of the currently open
/// <see cref="UIScreenDocument"/> and drives the shared
/// <see cref="UIScreenSelectionService"/>.
/// </summary>
public sealed class UIScreenHierarchyPanel
{
    private readonly MGWindow _window;
    private readonly UIScreenSelectionService _selection;

    private MGDockPanel? _root;
    private MGTreeView? _treeView;
    private MGTextBlock? _statusText;

    private UIScreenDocument? _document;
    private bool _suppressSelectionSync;

    private readonly Dictionary<MGTreeViewItem, DocumentNodeId> _itemToNode = new();
    private readonly Dictionary<DocumentNodeId, MGTreeViewItem> _nodeToItem = new();

    public UIScreenHierarchyPanel(MGWindow window, UIScreenSelectionService selection)
    {
        _window = window;
        _selection = selection;
        _selection.SelectionChanged += OnExternalSelectionChanged;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Loads (or clears) the displayed document tree.</summary>
    public void SetDocument(UIScreenDocument? document)
    {
        _document = document;
        RebuildTree();
    }

    /// <summary>
    /// Fired after a node is deleted. Passes the modified document so that
    /// the caller can rebuild the preview.
    /// </summary>
    public event Action<UIScreenDocument>? NodeDeleted;

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
        };
        _treeView.SelectionChanged += OnTreeSelectionChanged;

        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Margin = new Thickness(4, 2, 4, 2),
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        toolbar.TryAddChild(new MGTextBlock(_window, "[b]Hierarchy[/b]")
        {
            VerticalAlignment = VerticalAlignment.Center,
        });

        var refreshBtn = new MGButton(_window, _ => RebuildTree())
        {
            Padding = new Thickness(4, 1, 4, 1),
        };
        refreshBtn.SetContent(new MGTextBlock(_window, "⟳"));
        toolbar.TryAddChild(refreshBtn);
        var deleteBtn = new MGButton(_window, _ => DeleteSelectedNode())
        {
            Padding = new Thickness(4, 1, 4, 1),
            IsEnabled = false,
        };
        deleteBtn.SetContent(new MGTextBlock(_window, "\u2715"));
        toolbar.TryAddChild(deleteBtn);

        // Enable/disable delete button based on selection
        _selection.SelectionChanged += id => deleteBtn.IsEnabled = id.HasValue && _document != null;
        _statusText = new MGTextBlock(_window, "No screen loaded")
        {
            Margin = new Thickness(6, 4, 6, 4),
            Opacity = 0.75f,
        };

        var scrollViewer = new MGScrollViewer(_window);
        scrollViewer.SetContent(_treeView);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Bottom);
        _root.TryAddChild(scrollViewer, Dock.Top);

        RebuildTree();
        return _root;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Tree building
    // ─────────────────────────────────────────────────────────────────────

    private void RebuildTree()
    {
        if (_treeView == null)
        {
            return;
        }

        _itemToNode.Clear();
        _nodeToItem.Clear();
        _treeView.ClearItems();

        if (_document?.Root == null)
        {
            if (_statusText != null)
            {
                _statusText.Text = "No screen loaded";
            }

            return;
        }

        var rootItem = BuildTreeItem(_document.Root);
        rootItem.IsExpanded = true;
        _treeView.AddItem(rootItem);

        if (_statusText != null)
        {
            _statusText.Text = string.Empty;
        }

        // Restore selection after rebuild
        if (_selection.SelectedNodeId.HasValue && _nodeToItem.TryGetValue(_selection.SelectedNodeId.Value, out var toReselect))
        {
            _suppressSelectionSync = true;
            _treeView.SelectItem(toReselect);
            _suppressSelectionSync = false;
        }
    }

    private MGTreeViewItem BuildTreeItem(UIScreenNode node)
    {
        var item = new MGTreeViewItem(_window)
        {
            IsExpanded = true,
            Header = BuildNodeHeader(node),
        };

        _itemToNode[item] = node.Id;
        _nodeToItem[node.Id] = item;

        foreach (var child in node.Children)
        {
            item.AddItem(BuildTreeItem(child));
        }

        return item;
    }

    private MGElement BuildNodeHeader(UIScreenNode node)
    {
        var label = string.IsNullOrWhiteSpace(node.Name)
            ? node.ControlType
            : $"{node.ControlType}  [italic][opacity=0.65]{EscapeMarkup(node.Name)}[/opacity][/italic]";

        return new MGTextBlock(_window, label)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Selection sync
    // ─────────────────────────────────────────────────────────────────────

    private void OnTreeSelectionChanged(object? sender, MGTreeViewItem item)
    {
        if (_suppressSelectionSync)
        {
            return;
        }

        if (item != null && _itemToNode.TryGetValue(item, out var nodeId))
        {
            _suppressSelectionSync = true;
            _selection.Select(nodeId);
            _suppressSelectionSync = false;
        }
        else
        {
            _suppressSelectionSync = true;
            _selection.ClearSelection();
            _suppressSelectionSync = false;
        }
    }

    private void OnExternalSelectionChanged(DocumentNodeId? nodeId)
    {
        if (_suppressSelectionSync || _treeView == null)
        {
            return;
        }

        _suppressSelectionSync = true;

        if (nodeId.HasValue && _nodeToItem.TryGetValue(nodeId.Value, out var item))
        {
            _treeView.SelectItem(item);
        }
        else
        {
            _treeView.ClearSelection();
        }

        _suppressSelectionSync = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Deletion
    // ─────────────────────────────────────────────────────────────────────

    private void DeleteSelectedNode()
    {
        if (_document == null || !_selection.SelectedNodeId.HasValue)
        {
            return;
        }

        var nodeId = _selection.SelectedNodeId.Value;
        var node = _document.FindNode(nodeId);
        if (node == null)
        {
            return;
        }

        _selection.ClearSelection();

        if (node.Parent != null)
        {
            node.Parent.RemoveChild(node);
        }
        else
        {
            // Deleting the root
            _document.ClearRoot();
        }

        RebuildTree();
        NodeDeleted?.Invoke(_document);
    }

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}
