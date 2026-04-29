using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

public sealed class EntityAssetHierarchyPanel
{
    private readonly MGWindow _window;
    private readonly Dictionary<MGTreeViewItem, EntityComponent> _itemToComponent = new();
    private readonly Dictionary<EntityComponent, MGTreeViewItem> _componentToItem = new();

    private MGDockPanel? _root;
    private MGTreeView? _treeView;
    private MGTextBlock? _summaryText;
    private MGTreeViewItem? _entityRootItem;
    private EntityAssetEditorPanel? _editorPanel;
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
            _editorPanel.SelectedComponentChanged -= OnSelectedComponentChanged;
        }

        _editorPanel = editorPanel;

        if (_editorPanel != null)
        {
            _editorPanel.SelectedComponentChanged += OnSelectedComponentChanged;
        }

        Refresh();
    }

    public void Refresh()
    {
        RebuildTree();
        ApplySelection(_editorPanel?.SelectedComponent);
    }

    private void OnSelectedComponentChanged(EntityComponent? component)
    {
        ApplySelection(component);
    }

    private void OnTreeSelectionChanged(object? sender, MGTreeViewItem? item)
    {
        if (_suppressSelectionChanged || _editorPanel == null)
        {
            return;
        }

        if (ReferenceEquals(item, _entityRootItem))
        {
            _editorPanel.SetSelectedComponent(null);
            return;
        }

        _editorPanel.SetSelectedComponent(item != null && _itemToComponent.TryGetValue(item, out var component)
            ? component
            : null);
    }

    private void RebuildTree()
    {
        if (_treeView == null)
        {
            return;
        }

        _itemToComponent.Clear();
        _componentToItem.Clear();
        _treeView.ClearItems();
        _entityRootItem = null;

        var entity = _editorPanel?.LoadedEntity;
        if (entity == null)
        {
            UpdateSummary(null);
            return;
        }

        _entityRootItem = new MGTreeViewItem(_window)
        {
            IsExpanded = true,
            Header = BuildEntityHeader(entity),
        };

        if (entity.RootComponent != null)
        {
            _entityRootItem.AddItem(BuildComponentTreeItem(entity.RootComponent));
        }

        foreach (var component in entity.Components.OrderBy(GetComponentLabel, StringComparer.OrdinalIgnoreCase))
        {
            _entityRootItem.AddItem(BuildComponentTreeItem(component));
        }

        _treeView.AddItem(_entityRootItem);
        UpdateSummary(entity);
    }

    private void ApplySelection(EntityComponent? component)
    {
        if (_treeView == null)
        {
            return;
        }

        _suppressSelectionChanged = true;
        try
        {
            if (component == null)
            {
                if (_entityRootItem != null)
                {
                    _treeView.SelectItem(_entityRootItem);
                    _treeView.ScrollIntoView(_entityRootItem);
                }
                else
                {
                    _treeView.ClearSelection();
                }

                return;
            }

            if (_componentToItem.TryGetValue(component, out var item))
            {
                _treeView.SelectItem(item);
                _treeView.ScrollIntoView(item);
            }
            else if (_entityRootItem != null)
            {
                _treeView.SelectItem(_entityRootItem);
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

    private MGTreeViewItem BuildComponentTreeItem(EntityComponent component)
    {
        var item = new MGTreeViewItem(_window)
        {
            IsExpanded = true,
        };

        item.Header = BuildComponentHeader(component, item);
        _itemToComponent[item] = component;
        _componentToItem[component] = item;

        if (component is SceneComponent sceneComponent)
        {
            foreach (var child in sceneComponent.Children.OrderBy(GetComponentLabel, StringComparer.OrdinalIgnoreCase))
            {
                item.AddItem(BuildComponentTreeItem(child));
            }
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

    private MGElement BuildComponentHeader(EntityComponent component, MGTreeViewItem item)
    {
        var header = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.MouseHandler.LMBClickedInside += (_, e) => OnComponentHeaderClicked(item, e.ClickCount);

        var icon = EditorIcons.Box ?? EditorIcons.Layers ?? EditorIcons.ListTree;
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

        header.TryAddChild(new MGTextBlock(_window, GetComponentLabel(component))
        {
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        });

        return header;
    }

    private void OnComponentHeaderClicked(MGTreeViewItem item, int clickCount)
    {
        _treeView?.SelectItem(item);
        _treeView?.Focus();

        if (item.HasItems && clickCount == 1)
        {
            item.IsExpanded = !item.IsExpanded;
        }
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

        int componentCount = CountComponents(entity);
        _summaryText.SetText($"{componentCount} component{(componentCount == 1 ? string.Empty : "s")}");
    }

    private static int CountComponents(Entity entity)
    {
        int count = 0;

        if (entity.RootComponent != null)
        {
            count += CountSceneComponents(entity.RootComponent);
        }

        foreach (var component in entity.Components)
        {
            count++;
            if (component is SceneComponent sceneComponent)
            {
                foreach (var child in sceneComponent.Children)
                {
                    count += CountSceneComponents(child);
                }
            }
        }

        return count;
    }

    private static int CountSceneComponents(SceneComponent component)
    {
        int count = 1;
        foreach (var child in component.Children)
        {
            count += CountSceneComponents(child);
        }

        return count;
    }

    private static string GetComponentLabel(EntityComponent component)
    {
        var displayName = component.GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
            ?? component.GetType().Name;
        var instanceName = component.Name?.Trim();

        if (string.IsNullOrWhiteSpace(instanceName)
            || string.Equals(instanceName, displayName, StringComparison.OrdinalIgnoreCase)
            || instanceName.StartsWith("Object ", StringComparison.OrdinalIgnoreCase))
        {
            return displayName;
        }

        return $"{displayName} [{instanceName}]";
    }
}