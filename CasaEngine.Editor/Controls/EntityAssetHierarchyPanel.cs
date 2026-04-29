using System;
using System.Collections.Generic;
using CasaEngine.Framework.Scene.Entities;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

public sealed class EntityAssetHierarchyPanel
{
    private readonly MGWindow _window;
    private readonly Dictionary<MGTreeViewItem, Entity> _itemToEntity = new();
    private readonly Dictionary<Entity, MGTreeViewItem> _entityToItem = new();
    private readonly HashSet<Guid> _expandedEntityIds = [];
    private readonly HashSet<Entity> _observedEntities = [];

    private MGDockPanel? _root;
    private MGTreeView? _treeView;
    private MGTextBlock? _summaryText;
    private MGTreeViewItem? _rootEntityItem;
    private EntityAssetEditorPanel? _editorPanel;
    private Entity? _observedRootEntity;
    private bool _suppressSelectionChanged;

    public EntityAssetHierarchyPanel(MGWindow window)
    {
        _window = window;
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

        _summaryText = new MGTextBlock(_window, "No entity loaded")
        {
            Margin = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center,
        };

        _root = new MGDockPanel(_window);
        _root.TryAddChild(_summaryText, Dock.Bottom);
        _root.TryAddChild(_treeView, Dock.Top);

        Refresh();
        return _root;
    }

    public void SetEditorPanel(EntityAssetEditorPanel? editorPanel)
    {
        if (ReferenceEquals(_editorPanel, editorPanel))
        {
            Refresh();
            return;
        }

        if (_editorPanel != null)
        {
            _editorPanel.SelectedEntityChanged -= OnSelectedEntityChanged;
        }

        _editorPanel = editorPanel;

        if (_editorPanel != null)
        {
            _editorPanel.SelectedEntityChanged += OnSelectedEntityChanged;
        }

        Refresh();
    }

    public void Refresh()
    {
        AttachEntityTree(_editorPanel?.LoadedEntity);
        RebuildTree();
        ApplySelection(_editorPanel?.SelectedEntity ?? _editorPanel?.LoadedEntity);
    }

    private void OnSelectedEntityChanged(Entity? entity)
    {
        ApplySelection(entity);
    }

    private void OnTreeSelectionChanged(object? sender, MGTreeViewItem? item)
    {
        if (_suppressSelectionChanged || _editorPanel == null)
        {
            return;
        }

        _editorPanel.SetSelectedEntity(item != null && _itemToEntity.TryGetValue(item, out var entity)
            ? entity
            : _editorPanel.LoadedEntity);
    }

    private void OnTreeItemExpanded(object? sender, MGTreeViewItem item)
    {
        if (_itemToEntity.TryGetValue(item, out var entity))
        {
            _expandedEntityIds.Add(entity.Id);
        }
    }

    private void OnTreeItemCollapsed(object? sender, MGTreeViewItem item)
    {
        if (_itemToEntity.TryGetValue(item, out var entity))
        {
            _expandedEntityIds.Remove(entity.Id);
        }
    }

    private void RebuildTree()
    {
        if (_treeView == null)
        {
            return;
        }

        _itemToEntity.Clear();
        _entityToItem.Clear();
        _treeView.ClearItems();
        _rootEntityItem = null;

        var entity = _editorPanel?.LoadedEntity;
        if (entity == null)
        {
            UpdateSummary(null);
            return;
        }

        _rootEntityItem = BuildTreeItem(entity);
        _rootEntityItem.IsExpanded = true;
        _treeView.AddItem(_rootEntityItem);
        UpdateSummary(entity);
    }

    private void ApplySelection(Entity? entity)
    {
        if (_treeView == null)
        {
            return;
        }

        _suppressSelectionChanged = true;
        try
        {
            if (entity == null)
            {
                if (_rootEntityItem != null)
                {
                    _treeView.SelectItem(_rootEntityItem);
                    _treeView.ScrollIntoView(_rootEntityItem);
                }
                else
                {
                    _treeView.ClearSelection();
                }

                return;
            }

            if (_entityToItem.TryGetValue(entity, out var item))
            {
                ExpandAncestors(item);
                _treeView.SelectItem(item);
                _treeView.ScrollIntoView(item);
            }
            else if (_rootEntityItem != null)
            {
                _treeView.SelectItem(_rootEntityItem);
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

    private MGTreeViewItem BuildTreeItem(Entity entity)
    {
        var item = new MGTreeViewItem(_window)
        {
            IsExpanded = _expandedEntityIds.Contains(entity.Id),
            Header = BuildEntityHeader(entity),
        };

        _itemToEntity[item] = entity;
        _entityToItem[entity] = item;

        foreach (var child in entity.Children)
        {
            item.AddItem(BuildTreeItem(child));
        }

        return item;
    }

    private MGElement BuildEntityHeader(Entity entity)
    {
        var header = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var icon = EditorIcons.ListTree ?? EditorIcons.Box ?? EditorIcons.Layers;
        if (icon != null)
        {
            header.TryAddChild(new MGImage(_window, EditorIcons.AsImage(icon)!, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 16,
                PreferredHeight = 16,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
            });
        }

        header.TryAddChild(new MGTextBlock(_window, string.IsNullOrWhiteSpace(entity.Name) ? "Entity" : entity.Name)
        {
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        });

        return header;
    }

    private void UpdateSummary(Entity? entity)
    {
        if (_summaryText == null)
        {
            return;
        }

        if (entity == null)
        {
            _summaryText.SetText("No entity loaded");
            return;
        }

        int entityCount = CountEntities(entity);
        _summaryText.SetText($"{entityCount} entit{(entityCount == 1 ? "y" : "ies")}");
    }

    private void AttachEntityTree(Entity? entity)
    {
        if (ReferenceEquals(_observedRootEntity, entity))
        {
            return;
        }

        DetachEntityTree();
        _observedRootEntity = entity;

        if (entity != null)
        {
            SubscribeEntityTree(entity);
        }
    }

    private void DetachEntityTree()
    {
        foreach (var entity in _observedEntities)
        {
            entity.ChildAdded -= OnEntityChildAdded;
            entity.ChildRemoved -= OnEntityChildRemoved;
            entity.NameChanged -= OnEntityNameChanged;
        }

        _observedEntities.Clear();
        _observedRootEntity = null;
    }

    private void SubscribeEntityTree(Entity entity)
    {
        if (!_observedEntities.Add(entity))
        {
            return;
        }

        entity.ChildAdded += OnEntityChildAdded;
        entity.ChildRemoved += OnEntityChildRemoved;
        entity.NameChanged += OnEntityNameChanged;

        foreach (var child in entity.Children)
        {
            SubscribeEntityTree(child);
        }
    }

    private void UnsubscribeEntityTree(Entity entity)
    {
        if (!_observedEntities.Remove(entity))
        {
            return;
        }

        entity.ChildAdded -= OnEntityChildAdded;
        entity.ChildRemoved -= OnEntityChildRemoved;
        entity.NameChanged -= OnEntityNameChanged;

        foreach (var child in entity.Children)
        {
            UnsubscribeEntityTree(child);
        }
    }

    private void OnEntityChildAdded(object? sender, Entity child)
    {
        SubscribeEntityTree(child);
        RebuildTree();
        ApplySelection(_editorPanel?.SelectedEntity ?? _editorPanel?.LoadedEntity);
    }

    private void OnEntityChildRemoved(object? sender, Entity child)
    {
        UnsubscribeEntityTree(child);
        RebuildTree();
        ApplySelection(_editorPanel?.SelectedEntity ?? _editorPanel?.LoadedEntity);
    }

    private void OnEntityNameChanged(object? sender, EntityNameChangedEventArgs e)
    {
        RebuildTree();
        ApplySelection(_editorPanel?.SelectedEntity ?? _editorPanel?.LoadedEntity);
    }

    private void ExpandAncestors(MGTreeViewItem item)
    {
        for (var parent = item.ParentItem; parent != null; parent = parent.ParentItem)
        {
            parent.IsExpanded = true;
        }
    }

    private static int CountEntities(Entity entity)
    {
        int count = 1;
        foreach (var child in entity.Children)
        {
            count += CountEntities(child);
        }

        return count;
    }
}