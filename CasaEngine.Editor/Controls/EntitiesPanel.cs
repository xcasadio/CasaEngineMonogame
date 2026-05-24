using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Workspaces;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.History;
using CasaEngine.EditorServices.Particles;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.DragDrop;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

public sealed class EntitiesPanel
{
    private readonly MGWindow _window;
    private readonly Func<World?> _getCurrentWorld;

    private readonly Dictionary<MGTreeViewItem, Entity> _itemToEntity = new();
    private readonly Dictionary<Entity, MGTreeViewItem> _entityToItem = new();
    private readonly HashSet<Guid> _expandedEntityIds = [];
    private readonly HashSet<Entity> _observedEntities = [];

    private MGDockPanel? _root;
    private MGTreeView? _treeView;
    private MGTreeViewItem? _worldRootItem;
    private MGTextBox? _searchBox;
    private MGTextBlock? _summaryText;
    private World? _currentWorld;
    private World? _selectedWorld;
    private Entity? _selectedEntity;
    private int _selectedEntityCount;
    private string _filterText = string.Empty;
    private bool _suppressSelectionChanged;

    public EntitiesPanel(MGWindow window, Func<World?> getCurrentWorld)
    {
        _window = window;
        _getCurrentWorld = getCurrentWorld;
    }

    public event Action<Entity?>? SelectedEntityChanged;

    public event Action<World?>? SelectedWorldChanged;

    public event Action<Entity>? EntityDoubleClicked;

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
        _treeView.ItemExpanded += OnTreeItemExpanded;
        _treeView.ItemCollapsed += OnTreeItemCollapsed;
        _treeView.ItemDoubleClicked += OnTreeItemDoubleClicked;
        _treeView.MouseHandler.RMBReleasedInside += OnTreeRightClick;

        var toolbar = BuildToolbar();

        _summaryText = new MGTextBlock(_window, "No world loaded")
        {
            Margin = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center,
        };

        _root = new MGDockPanel(_window);
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(_summaryText, Dock.Bottom);
        _root.TryAddChild(_treeView, Dock.Top);

        EditorProjectAuthoringService.ProjectLoaded += OnProjectLoaded;

        Update();
        return _root;
    }

    public void Update()
    {
        var world = _getCurrentWorld();
        if (!ReferenceEquals(_currentWorld, world))
        {
            AttachWorld(world);
            RebuildTree();
            return;
        }

        if (_root != null && _treeView != null && _treeView.Items.Count == 0 && _currentWorld != null)
        {
            RebuildTree();
        }
    }

    public void Refresh()
    {
        AttachWorld(_getCurrentWorld());
        RebuildTree();
    }

    public void SetSelectedEntity(Entity? entity)
    {
        SetSelectionState(null, entity, entity != null ? 1 : 0);
    }

    public void SetSelectedWorld(World? world)
    {
        SetSelectionState(world, null, world != null ? 1 : 0);
    }

    public void SetSelectionState(World? world, Entity? entity, int selectedEntityCount)
    {
        _selectedWorld = world;
        _selectedEntity = entity;
        _selectedEntityCount = selectedEntityCount;
        UpdateSummary();

        if (_treeView == null)
        {
            return;
        }

        _suppressSelectionChanged = true;
        try
        {
            if (entity == null)
            {
                if (world != null && _worldRootItem != null)
                {
                    _treeView.SelectItem(_worldRootItem);
                    _treeView.ScrollIntoView(_worldRootItem);
                }
                else
                {
                    _treeView.ClearSelection();
                }

                return;
            }

            if (!_entityToItem.TryGetValue(entity, out var item))
            {
                RebuildTree();
                if (!_entityToItem.TryGetValue(entity, out item))
                {
                    return;
                }
            }

            ExpandAncestors(item);
            _treeView.SelectItem(item);
            _treeView.ScrollIntoView(item);
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
    }

    private MGElement BuildToolbar()
    {
        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Margin = new Thickness(4, 4, 4, 2),
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var addButton = CreateIconButton(EditorIcons.FilePlus, _ => AddEntity(_selectedEntity));
        var duplicateButton = CreateIconButton(EditorIcons.CopyPlus ?? EditorIcons.Copy, _ => DuplicateSelectedEntity());
        var deleteButton = CreateIconButton(EditorIcons.Trash, _ => DeleteSelectedEntity());
        var refreshButton = CreateIconButton(EditorIcons.RefreshCw, _ => Refresh());

        _searchBox = new MGTextBox(_window)
        {
            PreferredWidth = 160,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _searchBox.SetText(_filterText);
        _searchBox.TextChanged += OnSearchTextChanged;

        toolbar.TryAddChild(addButton);
        toolbar.TryAddChild(duplicateButton);
        toolbar.TryAddChild(deleteButton);
        toolbar.TryAddChild(refreshButton);
        toolbar.TryAddChild(new MGTextBlock(_window, "Search")
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        });
        toolbar.TryAddChild(_searchBox);

        return toolbar;
    }

    private MGButton CreateIconButton(Texture2D? icon, Action<MGButton> onClick)
    {
        var button = new MGButton(_window, onClick)
        {
            PreferredWidth = 28,
            PreferredHeight = 28,
        };

        if (icon != null)
        {
            button.SetContent(new MGImage(_window, EditorIcons.AsImage(icon)!, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 16,
                PreferredHeight = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        return button;
    }

    private void OnProjectLoaded(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void OnSearchTextChanged(object? sender, MGUI.Shared.Helpers.EventArgs<string> e)
    {
        _filterText = e.NewValue?.Trim() ?? string.Empty;
        RebuildTree();
    }

    private void OnTreeSelectionChanged(object? sender, MGTreeViewItem item)
    {
        if (_suppressSelectionChanged)
        {
            return;
        }

        if (ReferenceEquals(item, _worldRootItem))
        {
            _selectedWorld = _currentWorld;
            _selectedEntity = null;
            _selectedEntityCount = _selectedWorld != null ? 1 : 0;
            UpdateSummary();
            SelectedWorldChanged?.Invoke(_selectedWorld);
            return;
        }

        _selectedWorld = null;
        _selectedEntity = _itemToEntity.TryGetValue(item, out var entity) ? entity : null;
        _selectedEntityCount = _selectedEntity != null ? 1 : 0;
        UpdateSummary();
        SelectedEntityChanged?.Invoke(_selectedEntity);
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

    private void OnTreeItemDoubleClicked(object? sender, MGTreeViewItem item)
    {
        if (_itemToEntity.TryGetValue(item, out var entity))
        {
            EntityDoubleClicked?.Invoke(entity);
        }
    }

    private void OnTreeRightClick(object? sender, BaseMouseReleasedEventArgs e)
    {
        var menu = new MGContextMenu(_window, null);
        var selectedEntity = _treeView?.SelectedItem != null && _itemToEntity.TryGetValue(_treeView.SelectedItem, out var entity)
            ? entity
            : null;

        menu.AddButton("Add Entity", _ => AddEntity(selectedEntity));

        if (selectedEntity != null)
        {
            menu.AddButton("Rename", _ => RenameEntity(selectedEntity));
            menu.AddButton("Duplicate", _ => DuplicateEntity(selectedEntity));
            menu.AddSeparator();
            menu.AddButton("Delete", _ => DeleteEntity(selectedEntity));
        }

        menu.AddSeparator();
        menu.AddButton("Refresh", _ => Refresh());
        _treeView?.GetDesktop().TryOpenContextMenu(menu, e.Position);
    }

    private void AttachWorld(World? world)
    {
        if (ReferenceEquals(_currentWorld, world))
        {
            return;
        }

        DetachWorld();
        _currentWorld = world;

        if (_currentWorld == null)
        {
            UpdateSummary();
            return;
        }

        _currentWorld.EntityAdded += OnWorldEntityAdded;
        _currentWorld.EntityRemoved += OnWorldEntityRemoved;
        _currentWorld.EntitiesClear += OnWorldEntitiesCleared;
        foreach (var entity in _currentWorld.Entities)
        {
            SubscribeEntityTree(entity);
        }

        UpdateSummary();
    }

    private void DetachWorld()
    {
        if (_currentWorld != null)
        {
            _currentWorld.EntityAdded -= OnWorldEntityAdded;
            _currentWorld.EntityRemoved -= OnWorldEntityRemoved;
            _currentWorld.EntitiesClear -= OnWorldEntitiesCleared;
        }

        foreach (var entity in _observedEntities.ToList())
        {
            UnsubscribeEntityTree(entity);
        }

        _currentWorld = null;
    }

    private void OnWorldEntityAdded(object? sender, Entity entity)
    {
        SubscribeEntityTree(entity);
        RebuildTree();
    }

    private void OnWorldEntityRemoved(object? sender, Entity entity)
    {
        UnsubscribeEntityTree(entity);
        if (ReferenceEquals(_selectedEntity, entity))
        {
            _selectedEntity = null;
            SelectedEntityChanged?.Invoke(null);
        }

        RebuildTree();
    }

    private void OnWorldEntitiesCleared(object? sender, EventArgs e)
    {
        foreach (var entity in _observedEntities.ToList())
        {
            UnsubscribeEntityTree(entity);
        }

        _selectedEntity = null;
        SelectedEntityChanged?.Invoke(null);
        RebuildTree();
    }

    private void SubscribeEntityTree(Entity entity)
    {
        if (!_observedEntities.Add(entity))
        {
            return;
        }

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

        entity.NameChanged -= OnEntityNameChanged;

        foreach (var child in entity.Children)
        {
            UnsubscribeEntityTree(child);
        }
    }

    private void OnEntityNameChanged(object? sender, EntityNameChangedEventArgs e)
    {
        RebuildTree();
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
        _worldRootItem = null;

        if (_currentWorld == null)
        {
            UpdateSummary();
            return;
        }

        _worldRootItem = new MGTreeViewItem(_window)
        {
            IsExpanded = true,
            Header = BuildWorldHeader(_currentWorld),
        };

        foreach (var entity in _currentWorld.Entities)
        {
            if (ShouldIncludeEntity(entity))
            {
                _worldRootItem.AddItem(BuildTreeItem(entity));
            }
        }

        _treeView.AddItem(_worldRootItem);

        UpdateSummary();
        SetSelectionState(_selectedWorld, _selectedEntity, _selectedEntityCount);
    }

    private MGElement BuildWorldHeader(World world)
    {
        var header = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var icon = EditorIcons.Layers ?? EditorIcons.ListTree ?? EditorIcons.Box;
        if (icon != null)
        {
            header.TryAddChild(new MGImage(_window, EditorIcons.AsImage(icon)!, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 16,
                PreferredHeight = 16,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        header.TryAddChild(new MGTextBlock(_window, string.IsNullOrWhiteSpace(world.Name) ? "World" : world.Name)
        {
            VerticalAlignment = VerticalAlignment.Center,
        });

        return header;
    }

    private MGTreeViewItem BuildTreeItem(Entity entity)
    {
        var item = new MGTreeViewItem(_window)
        {
            IsExpanded = !string.IsNullOrWhiteSpace(_filterText) || _expandedEntityIds.Contains(entity.Id),
        };
        item.Header = BuildEntityHeader(entity);

        _itemToEntity[item] = entity;
        _entityToItem[entity] = item;

        foreach (var child in entity.Children)
        {
            if (ShouldIncludeEntity(child))
            {
                item.AddItem(BuildTreeItem(child));
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
            AllowDrop = true,
        };
        header.DragEnter += (_, e) => OnEntityHeaderDrag(e);
        header.DragOver += (_, e) => OnEntityHeaderDrag(e);
        header.Drop += (_, e) => DropParticleAssetOnEntity(entity, e.Data.GetData<List<ContentItem>>());

        var icon = EditorIcons.ListTree ?? EditorIcons.Box ?? EditorIcons.Layers;
        if (icon != null)
        {
            header.TryAddChild(new MGImage(_window, EditorIcons.AsImage(icon)!, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 16,
                PreferredHeight = 16,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        header.TryAddChild(new MGTextBlock(_window, entity.Name)
        {
            VerticalAlignment = VerticalAlignment.Center,
        });

        return header;
    }

    private void OnEntityHeaderDrag(DragDropEventArgs e)
    {
        e.Data.DropEffect = TryResolveFirstParticleAsset(e.Data.GetData<List<ContentItem>>(), out _)
            ? DragDropEffect.Copy
            : DragDropEffect.None;
    }

    private void DropParticleAssetOnEntity(Entity entity, IReadOnlyList<ContentItem>? draggedItems)
    {
        if (!TryResolveFirstParticleAsset(draggedItems, out var assetInfo))
        {
            return;
        }

        var existingComponent = entity.GetComponent<ParticleSystemComponent>();
        var previousSelection = _selectedEntity;

        if (existingComponent != null)
        {
            Guid previousAssetId = existingComponent.ParticleEffectAssetId;
            ExecuteWorldCommand(
                "Update Particle System Component",
                () =>
                {
                    EditorParticleSystemComponentService.ApplyParticleAsset(entity, existingComponent, assetInfo.Id);
                    ApplySelectionAfterMutation(entity);
                },
                () =>
                {
                    EditorParticleSystemComponentService.ApplyParticleAsset(entity, existingComponent, previousAssetId);
                    ApplySelectionAfterMutation(previousSelection);
                });
            return;
        }

        var component = EditorParticleSystemComponentService.CreateParticleComponent(assetInfo.Id, Microsoft.Xna.Framework.Vector3.Zero);
        var attachment = EditorParticleSystemComponentService.CreateAttachment(entity);
        ExecuteWorldCommand(
            "Add Particle System Component",
            () =>
            {
                EditorParticleSystemComponentService.AttachComponent(entity, component, attachment);
                ApplySelectionAfterMutation(entity);
            },
            () =>
            {
                EditorParticleSystemComponentService.DetachComponent(entity, component, attachment);
                ApplySelectionAfterMutation(previousSelection);
            });
    }

    private bool TryResolveFirstParticleAsset(IReadOnlyList<ContentItem>? draggedItems, out AssetInfo assetInfo)
    {
        assetInfo = null!;
        if (_currentWorld == null || !AssetCatalog.IsLoaded || draggedItems == null || draggedItems.Count == 0)
        {
            return false;
        }

        for (int index = 0; index < draggedItems.Count; index++)
        {
            ContentItem item = draggedItems[index];
            if (item.IsDirectory || !string.Equals(item.Extension, Constants.FileNameExtensions.Particle, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryGetProjectRelativePath(item.FullPath, out var relativePath))
            {
                continue;
            }

            assetInfo = AssetCatalog.GetByFileName(relativePath)
                ?? AssetCatalog.GetByFileName(relativePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (assetInfo == null)
            {
                string normalizedRelativePath = NormalizeAssetPath(relativePath);
                foreach (var candidate in AssetCatalog.AssetInfos)
                {
                    if (string.Equals(NormalizeAssetPath(candidate.FileName), normalizedRelativePath, StringComparison.OrdinalIgnoreCase))
                    {
                        assetInfo = candidate;
                        break;
                    }
                }
            }

            if (assetInfo != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetProjectRelativePath(string fullPath, out string relativePath)
    {
        relativePath = string.Empty;

        string projectPath = EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath);
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return false;
        }

        string normalizedProjectPath = Path.GetFullPath(projectPath);
        string normalizedFullPath = Path.GetFullPath(fullPath);
        string projectRootWithSeparator = normalizedProjectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!normalizedFullPath.StartsWith(projectRootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedFullPath, normalizedProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        relativePath = Path.GetRelativePath(normalizedProjectPath, normalizedFullPath);
        return !string.IsNullOrWhiteSpace(relativePath);
    }

    private static string NormalizeAssetPath(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
    }

    private bool ShouldIncludeEntity(Entity entity)
    {
        if (string.IsNullOrWhiteSpace(_filterText))
        {
            return true;
        }

        if (entity.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return entity.Children.Any(ShouldIncludeEntity);
    }

    private void ExpandAncestors(MGTreeViewItem item)
    {
        var current = item.ParentItem;
        while (current != null)
        {
            current.IsExpanded = true;
            if (_itemToEntity.TryGetValue(current, out var entity))
            {
                _expandedEntityIds.Add(entity.Id);
            }

            current = current.ParentItem;
        }
    }

    private void UpdateSummary()
    {
        if (_summaryText == null)
        {
            return;
        }

        if (_currentWorld == null)
        {
            _summaryText.SetText("No world loaded");
            return;
        }

        int entityCount = EnumerateEntities(_currentWorld.Entities).Count();
        if (_selectedWorld != null && _selectedEntity == null)
        {
            _summaryText.SetText($"{entityCount} entit{(entityCount == 1 ? "y" : "ies")} - world selected");
            return;
        }

        if (_selectedEntityCount > 0)
        {
            _summaryText.SetText($"{entityCount} entit{(entityCount == 1 ? "y" : "ies")} - {_selectedEntityCount} selected");
            return;
        }

        _summaryText.SetText($"{entityCount} entit{(entityCount == 1 ? "y" : "ies")}");
    }

    private IEnumerable<Entity> EnumerateEntities(IEnumerable<Entity> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in EnumerateEntities(root.Children))
            {
                yield return child;
            }
        }
    }

    private void AddEntity(Entity? parent)
    {
        if (_currentWorld == null)
        {
            return;
        }

        var world = _currentWorld;
        var previousSelection = _selectedEntity;
        var entity = new Entity
        {
            Name = GenerateUniqueEntityName("Entity"),
        };

        ExecuteWorldCommand(
            "Add Entity",
            () =>
            {
                EditorWorldEditingService.AttachEntity(world, entity, parent);
                ApplySelectionAfterMutation(entity);
            },
            () =>
            {
                EditorWorldEditingService.DetachEntity(world, entity);
                ApplySelectionAfterMutation(previousSelection);
            });
    }

    private void DuplicateSelectedEntity()
    {
        if (_selectedEntity != null)
        {
            DuplicateEntity(_selectedEntity);
        }
    }

    private void DuplicateEntity(Entity entity)
    {
        if (_currentWorld == null)
        {
            return;
        }

        var world = _currentWorld;
        var parent = entity.Parent;
        var previousSelection = _selectedEntity;
        var duplicate = entity.Clone();
        duplicate.Name = GenerateUniqueEntityName($"{entity.Name} Copy");

        ExecuteWorldCommand(
            "Duplicate Entity",
            () =>
            {
                EditorWorldEditingService.AttachEntity(world, duplicate, parent);
                ApplySelectionAfterMutation(duplicate);
            },
            () =>
            {
                EditorWorldEditingService.DetachEntity(world, duplicate);
                ApplySelectionAfterMutation(previousSelection ?? entity);
            });
    }

    private void DeleteSelectedEntity()
    {
        if (_selectedEntity != null)
        {
            DeleteEntity(_selectedEntity);
        }
    }

    private void DeleteEntity(Entity entity)
    {
        if (_currentWorld == null)
        {
            return;
        }

        var world = _currentWorld;
        var parent = entity.Parent;
        var nextSelection = parent;

        ExecuteWorldCommand(
            "Delete Entity",
            () =>
            {
                EditorWorldEditingService.DetachEntity(world, entity);
                ApplySelectionAfterMutation(nextSelection);
            },
            () =>
            {
                EditorWorldEditingService.AttachEntity(world, entity, parent);
                ApplySelectionAfterMutation(entity);
            });
    }

    private void RenameEntity(Entity entity)
    {
        ShowNameEditor(
            "Rename Entity",
            entity.Name,
            value =>
            {
                var previousName = entity.Name;
                if (string.Equals(previousName, value, StringComparison.Ordinal))
                {
                    return;
                }

                ExecuteWorldCommand(
                    "Rename Entity",
                    () =>
                    {
                        entity.Name = value;
                        ApplySelectionAfterMutation(entity);
                    },
                    () =>
                    {
                        entity.Name = previousName;
                        ApplySelectionAfterMutation(entity);
                    });
            });
    }

    private void ExecuteWorldCommand(string description, Action execute, Action undo)
    {
        EditorHistoryService.Current.Execute(
            new EditorHistoryContext(EditorHistoryContextKind.World, EditorPanelIds.WorldViewport),
            new EditorDelegateCommand(description, execute, undo));
    }

    private void ApplySelectionAfterMutation(Entity? entity)
    {
        _selectedWorld = null;
        _selectedEntity = entity;
        _selectedEntityCount = entity != null ? 1 : 0;
        SelectedEntityChanged?.Invoke(entity);
        RebuildTree();
        SetSelectedEntity(entity);
    }

    private void ShowNameEditor(string title, string initialValue, Action<string> onConfirm)
    {
        if (_window.Desktop == null)
        {
            return;
        }

        const int width = 360;
        const int height = 140;
        var dialog = EditorModalDialogHelper.CreateCenteredModalWindow(_window, width, height, title);

        var stack = new MGStackPanel(dialog, Orientation.Vertical)
        {
            Spacing = 8,
            Padding = new Thickness(8),
        };

        var textBox = new MGTextBox(dialog);
        textBox.SetText(initialValue);

        var buttonRow = new MGStackPanel(dialog, Orientation.Horizontal)
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var okButton = new MGButton(dialog, _ =>
        {
            var value = textBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            onConfirm(value);
            dialog.TryCloseWindow();
        });
        okButton.SetContent(new MGTextBlock(dialog, "OK"));
        okButton.PreferredWidth = 80;

        var cancelButton = new MGButton(dialog, _ => dialog.TryCloseWindow());
        cancelButton.SetContent(new MGTextBlock(dialog, "Cancel"));
        cancelButton.PreferredWidth = 80;

        buttonRow.TryAddChild(okButton);
        buttonRow.TryAddChild(cancelButton);

        stack.TryAddChild(new MGTextBlock(dialog, "Name")
        {
            VerticalAlignment = VerticalAlignment.Center,
        });
        stack.TryAddChild(textBox);
        stack.TryAddChild(buttonRow);

        dialog.SetContent(stack);
        _window.Desktop.Windows.Add(dialog);
    }

    private string GenerateUniqueEntityName(string baseName)
    {
        if (_currentWorld == null)
        {
            return baseName;
        }

        var existingNames = EnumerateEntities(_currentWorld.Entities)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existingNames.Contains(baseName))
        {
            return baseName;
        }

        for (int index = 2; index < 10000; index++)
        {
            var candidate = $"{baseName} ({index})";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{baseName} ({Guid.NewGuid():N})";
    }
}