using System;
using System.Collections.Generic;
using CasaEngine.EditorServices.ScreenEditor.Commands;
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
    private UICommandStack? _commandStack;

    private MGDockPanel? _root;
    private MGTreeView? _treeView;
    private MGTextBlock? _statusText;
    private MGTextBox? _filterBox;
    private MGTextBlock? _breadcrumbText;  // Q-07

    private UIScreenDocument? _document;
    private bool _suppressSelectionSync;
    private bool _rebuildPending;
    private string _filterText = string.Empty;

    // R-04: Tree snapshot for diffing (DFS list of node id + label)
    private readonly record struct NodeSnapshot(DocumentNodeId Id, string ControlType, string? Name);
    private List<NodeSnapshot> _treeSnapshot = new();

    private readonly Dictionary<MGTreeViewItem, DocumentNodeId> _itemToNode = new();
    private readonly Dictionary<DocumentNodeId, MGTreeViewItem> _nodeToItem = new();

    public UIScreenHierarchyPanel(MGWindow window, UIScreenSelectionService selection)
    {
        _window = window;
        _selection = selection;
        _selection.SelectionChanged += OnExternalSelectionChanged;
    }

    /// <summary>Attaches a command stack so deletions are undoable.</summary>
    public void SetCommandStack(UICommandStack commandStack) => _commandStack = commandStack;

    // ─────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Loads (or clears) the displayed document tree.</summary>
    public void SetDocument(UIScreenDocument? document)
    {
        // R-04: Skip full rebuild when tree structure is unchanged (only property values changed).
        var newSnapshot = BuildSnapshot(document);
        if (ReferenceEquals(document, _document) && SnapshotsEqual(_treeSnapshot, newSnapshot))
        {
            // Tree structure identical — restore selection only, no rebuild needed.
            RestoreTreeSelection();
            UpdateBreadcrumb(_selection.SelectedNodeId);
            return;
        }

        _document = document;
        _treeSnapshot = newSnapshot;
        ScheduleRebuildTree();
    }

    /// <summary>
    /// Fired after a node is deleted. Passes the modified document so that
    /// the caller can rebuild the preview.
    /// </summary>
    public event Action<UIScreenDocument>? NodeDeleted;

    /// <summary>Fired when the user requests a node duplication via the context menu.</summary>
    public event Action<UIScreenDocument, DocumentNodeId>? NodeDuplicateRequested;

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

        var refreshBtn = new MGButton(_window, _ => ScheduleRebuildTree())
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

        // Q-05: Search / filter box
        _filterBox = new MGTextBox(_window)
        {
            PlaceholderText = "Filter…",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(4, 2, 4, 2),
        };
        _filterBox.TextChanged += (_, args) =>
        {
            _filterText = args.NewValue?.Trim() ?? string.Empty;
            ScheduleRebuildTree();
        };

        // Q-07: Breadcrumb display
        _breadcrumbText = new MGTextBlock(_window, string.Empty)
        {
            Opacity = 0.7f,
            WrapText = true,
            Margin = new Thickness(4, 1, 4, 1),
            FontSize = 10,
        };

        _statusText = new MGTextBlock(_window, "No screen loaded")
        {
            Margin = new Thickness(6, 4, 6, 4),
            Opacity = 0.75f,
        };

        var scrollViewer = new MGScrollViewer(_window);
        scrollViewer.SetContent(_treeView);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(_filterBox, Dock.Top);
        _root.TryAddChild(_breadcrumbText, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Bottom);
        _root.TryAddChild(scrollViewer, Dock.Top);

        if (_rebuildPending || _document != null)
        {
            _rebuildPending = false;
            RebuildTree();
        }
        return _root;
    }

    private void ScheduleRebuildTree()
    {
        if (_rebuildPending)
        {
            return;
        }

        _rebuildPending = true;

        if (_root == null)
        {
            return;
        }

        _root.InvokeLater(() =>
        {
            _rebuildPending = false;
            RebuildTree();
        }, 1, MGElement.InvokeLaterPriority.OnEndUpdate);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  R-04: Snapshot helpers
    // ─────────────────────────────────────────────────────────────────────

    private static List<NodeSnapshot> BuildSnapshot(UIScreenDocument? document)
    {
        var result = new List<NodeSnapshot>();
        if (document?.Root != null)
        {
            CollectSnapshot(document.Root, result);
        }

        return result;
    }

    private static void CollectSnapshot(UIScreenNode node, List<NodeSnapshot> result)
    {
        result.Add(new NodeSnapshot(node.Id, node.ControlType, node.Name));
        foreach (var child in node.Children)
        {
            CollectSnapshot(child, result);
        }
    }

    private static bool SnapshotsEqual(List<NodeSnapshot> a, List<NodeSnapshot> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
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

        if (string.IsNullOrWhiteSpace(_filterText))
        {
            var rootItem = BuildTreeItem(_document.Root);
            rootItem.IsExpanded = true;
            _treeView.AddItem(rootItem);

            if (_statusText != null)
            {
                _statusText.Text = string.Empty;
            }
        }
        else
        {
            // Q-05: Filter mode — build a flat list of matching nodes (DFS)
            var matches = new List<UIScreenNode>();
            CollectMatching(_document.Root, _filterText, matches);

            foreach (var node in matches)
            {
                var item = BuildTreeItem(node, shallow: true);
                _treeView.AddItem(item);
            }

            if (_statusText != null)
            {
                _statusText.Text = matches.Count == 0 ? "No matches." : $"{matches.Count} result(s)";
            }
        }

        RestoreTreeSelection();
        UpdateBreadcrumb(_selection.SelectedNodeId);
    }

    private static void CollectMatching(UIScreenNode node, string filter, List<UIScreenNode> result)
    {
        var label = string.IsNullOrWhiteSpace(node.Name)
            ? node.ControlType
            : $"{node.ControlType} {node.Name}";

        if (label.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            result.Add(node);
        }

        foreach (var child in node.Children)
        {
            CollectMatching(child, filter, result);
        }
    }

    private MGTreeViewItem BuildTreeItem(UIScreenNode node, bool shallow = false)
    {
        var item = new MGTreeViewItem(_window)
        {
            IsExpanded = true,
            Header = BuildNodeHeader(node),
        };

        _itemToNode[item] = node.Id;
        _nodeToItem[node.Id] = item;

        if (!shallow)
        {
            foreach (var child in node.Children)
            {
                item.AddItem(BuildTreeItem(child));
            }
        }

        return item;
    }

    private MGElement BuildNodeHeader(UIScreenNode node)
    {
        var label = string.IsNullOrWhiteSpace(node.Name)
            ? node.ControlType
            : $"{node.ControlType}  [italic][opacity=0.65]{EscapeMarkup(node.Name)}[/opacity][/italic]";

        var textBlock = new MGTextBlock(_window, label)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };

        // Q-04: Wire right-click context menu on each header
        textBlock.ContextMenuRequested += (sender, args) =>
        {
            args.Menu = BuildNodeContextMenu(node);
        };

        return textBlock;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Q-04: Context menu
    // ─────────────────────────────────────────────────────────────────────

    private MGContextMenu BuildNodeContextMenu(UIScreenNode node)
    {
        var menu = new MGContextMenu(_window);

        menu.AddButton("Select", _ => _selection.Select(node.Id));

        menu.AddButton("Duplicate", _ =>
        {
            if (_document != null)
                NodeDuplicateRequested?.Invoke(_document, node.Id);
        });

        menu.AddSeparator();

        menu.AddButton("Delete", _ =>
        {
            _selection.Select(node.Id);
            DeleteSelectedNode();
        });

        return menu;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Q-07: Breadcrumb
    // ─────────────────────────────────────────────────────────────────────

    private void UpdateBreadcrumb(DocumentNodeId? nodeId)
    {
        if (_breadcrumbText == null)
        {
            return;
        }

        if (!nodeId.HasValue || _document == null)
        {
            _breadcrumbText.Text = string.Empty;
            return;
        }

        var path = BuildBreadcrumbPath(_document, nodeId.Value);
        _breadcrumbText.Text = path;
    }

    private static string BuildBreadcrumbPath(UIScreenDocument document, DocumentNodeId nodeId)
    {
        var chain = new List<string>();
        var node = document.FindNode(nodeId);
        while (node != null)
        {
            var segment = string.IsNullOrWhiteSpace(node.Name) ? node.ControlType : $"{node.ControlType}[{node.Name}]";
            chain.Add(segment);
            node = node.Parent;
        }

        chain.Reverse();
        return string.Join(" › ", chain);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Selection sync
    // ─────────────────────────────────────────────────────────────────────

    private void RestoreTreeSelection()
    {
        if (_treeView == null)
        {
            return;
        }

        if (_selection.SelectedNodeId.HasValue && _nodeToItem.TryGetValue(_selection.SelectedNodeId.Value, out var toReselect))
        {
            _suppressSelectionSync = true;
            _treeView.SelectItem(toReselect);
            _suppressSelectionSync = false;
        }
    }

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
            UpdateBreadcrumb(nodeId);
        }
        else
        {
            _suppressSelectionSync = true;
            _selection.ClearSelection();
            _suppressSelectionSync = false;
            UpdateBreadcrumb(null);
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
        UpdateBreadcrumb(nodeId);
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

        if (_commandStack != null)
        {
            _commandStack.Execute(new RemoveNodeCommand(_document, node));
        }
        else if (node.Parent != null)
        {
            node.Parent.RemoveChild(node);
        }
        else
        {
            _document.ClearRoot();
        }

        _treeSnapshot = BuildSnapshot(_document);
        ScheduleRebuildTree();
        NodeDeleted?.Invoke(_document);
    }

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}

