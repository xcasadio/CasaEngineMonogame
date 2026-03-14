using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using CasaEngine.Editor.Controls.ComponentEditors;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

public sealed class EntityDetailsPanel
{
    private static readonly Lazy<IReadOnlyDictionary<string, Type>> ComponentTypes = new(CreateComponentTypeLookup);

    private readonly MGWindow _window;
    private readonly Dictionary<MGTreeViewItem, EntityComponent> _itemToComponent = new();
    private readonly Dictionary<EntityComponent, MGTreeViewItem> _componentToItem = new();

    private MGDockPanel? _root;
    private MGTextBox? _entityNameTextBox;
    private MGTreeView? _componentTree;
    private MGScrollViewer? _detailsScrollViewer;
    private MGStackPanel? _detailsContent;
    private MGTextBlock? _componentSummaryText;
    private Entity? _selectedEntity;
    private EntityComponent? _selectedComponent;
    private bool _suppressEntityNameChanged;
    private bool _suppressComponentSelectionChanged;

    public EntityDetailsPanel(MGWindow window)
    {
        _window = window;
    }

    public event Action<EntityComponent?>? SelectedComponentChanged;

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        var toolbar = BuildToolbar();

        _componentTree = new MGTreeView(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            PreferredHeight = 220,
            MinHeight = 140,
        };
        _componentTree.SelectionChanged += OnComponentTreeSelectionChanged;

        _componentSummaryText = new MGTextBlock(_window, "No entity selected")
        {
            Margin = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center,
        };

        _detailsContent = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 8,
            Padding = new Thickness(6),
        };

        _detailsScrollViewer = new MGScrollViewer(_window);
        _detailsScrollViewer.SetContent(_detailsContent);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(_componentSummaryText, Dock.Bottom);
        _root.TryAddChild(_componentTree, Dock.Top);
        _root.TryAddChild(_detailsScrollViewer, Dock.Top);

        RefreshEntityHeader();
        RebuildComponentTree();
        RebuildPropertyEditors();
        return _root;
    }

    public void SetSelectedEntity(Entity? entity)
    {
        if (ReferenceEquals(_selectedEntity, entity))
        {
            RefreshEntityHeader();
            RebuildPropertyEditors();
            return;
        }

        DetachEntity();
        _selectedEntity = entity;
        _selectedComponent = null;
        AttachEntity();
        RefreshEntityHeader();
        RebuildComponentTree();
        RebuildPropertyEditors();
    }

    public void SetSelectedComponent(EntityComponent? component)
    {
        _selectedComponent = component;

        if (_componentTree == null)
        {
            return;
        }

        _suppressComponentSelectionChanged = true;
        try
        {
            if (component == null)
            {
                _componentTree.ClearSelection();
                return;
            }

            if (!_componentToItem.TryGetValue(component, out var item))
            {
                _selectedComponent = null;
                _componentTree.ClearSelection();
                return;
            }

            _componentTree.SelectItem(item);
            _componentTree.ScrollIntoView(item);
        }
        finally
        {
            _suppressComponentSelectionChanged = false;
        }

        RebuildPropertyEditors();
    }

    private MGElement BuildToolbar()
    {
        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Margin = new Thickness(4, 4, 4, 2),
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };

        toolbar.TryAddChild(new MGTextBlock(_window, "Entity")
        {
            VerticalAlignment = VerticalAlignment.Center,
            PreferredWidth = 44,
        });

        _entityNameTextBox = new MGTextBox(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PreferredWidth = 180,
        };
        _entityNameTextBox.TextChanged += OnEntityNameChanged;

        var addComponentButton = new MGButton(_window, _ => ShowAddComponentDialog())
        {
            PreferredWidth = 34,
            PreferredHeight = 28,
        };
        if (EditorIcons.FilePlus != null)
        {
            addComponentButton.SetContent(new MGImage(_window, EditorIcons.FilePlus, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 16,
                PreferredHeight = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        else
        {
            addComponentButton.SetContent(new MGTextBlock(_window, "+")
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        toolbar.TryAddChild(_entityNameTextBox);
        toolbar.TryAddChild(addComponentButton);
        return toolbar;
    }

    private void AttachEntity()
    {
        if (_selectedEntity == null)
        {
            return;
        }

        _selectedEntity.NameChanged += OnSelectedEntityNameChanged;
        _selectedEntity.ComponentAdded += OnEntityComponentChanged;
        _selectedEntity.ComponentRemoved += OnEntityComponentChanged;
    }

    private void DetachEntity()
    {
        if (_selectedEntity == null)
        {
            return;
        }

        _selectedEntity.NameChanged -= OnSelectedEntityNameChanged;
        _selectedEntity.ComponentAdded -= OnEntityComponentChanged;
        _selectedEntity.ComponentRemoved -= OnEntityComponentChanged;
    }

    private void OnSelectedEntityNameChanged(object? sender, EntityNameChangedEventArgs e)
    {
        RefreshEntityHeader();
    }

    private void OnEntityComponentChanged(object? sender, EntityComponent component)
    {
        RebuildComponentTree();
        RebuildPropertyEditors();
    }

    private void OnEntityNameChanged(object? sender, EventArgs<string> e)
    {
        if (_suppressEntityNameChanged || _selectedEntity == null)
        {
            return;
        }

        var newName = e.NewValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newName) || newName == _selectedEntity.Name)
        {
            return;
        }

        _selectedEntity.Name = newName;
    }

    private void RefreshEntityHeader()
    {
        if (_entityNameTextBox == null)
        {
            return;
        }

        _suppressEntityNameChanged = true;
        _entityNameTextBox.SetText(_selectedEntity?.Name ?? string.Empty);
        _suppressEntityNameChanged = false;
    }

    private void RebuildComponentTree()
    {
        if (_componentTree == null)
        {
            return;
        }

        _itemToComponent.Clear();
        _componentToItem.Clear();
        _componentTree.ClearItems();

        if (_selectedEntity == null)
        {
            UpdateSummary();
            SetSelectedComponent(null);
            return;
        }

        if (_selectedEntity.RootComponent != null)
        {
            _componentTree.AddItem(BuildComponentTreeItem(_selectedEntity.RootComponent));
        }

        foreach (var component in _selectedEntity.Components.OrderBy(GetDisplayName, StringComparer.OrdinalIgnoreCase))
        {
            _componentTree.AddItem(BuildComponentTreeItem(component));
        }

        UpdateSummary();
        SetSelectedComponent(_selectedComponent);
    }

    private MGTreeViewItem BuildComponentTreeItem(EntityComponent component)
    {
        var item = new MGTreeViewItem(_window)
        {
            IsExpanded = true,
        };

        item.Header = BuildComponentHeader(component);
        _itemToComponent[item] = component;
        _componentToItem[component] = item;

        if (component is SceneComponent sceneComponent)
        {
            foreach (var child in sceneComponent.Children.OrderBy(GetDisplayName, StringComparer.OrdinalIgnoreCase))
            {
                item.AddItem(BuildComponentTreeItem(child));
            }
        }

        return item;
    }

    private MGElement BuildComponentHeader(EntityComponent component)
    {
        var header = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var icon = EditorIcons.Box ?? EditorIcons.Layers ?? EditorIcons.ListTree;
        if (icon != null)
        {
            header.TryAddChild(new MGImage(_window, icon, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 16,
                PreferredHeight = 16,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        header.TryAddChild(new MGTextBlock(_window, GetDisplayName(component))
        {
            VerticalAlignment = VerticalAlignment.Center,
        });

        return header;
    }

    private void OnComponentTreeSelectionChanged(object? sender, MGTreeViewItem item)
    {
        if (_suppressComponentSelectionChanged)
        {
            return;
        }

        _selectedComponent = _itemToComponent.TryGetValue(item, out var component) ? component : null;
        RebuildPropertyEditors();

        SelectedComponentChanged?.Invoke(_selectedComponent);
    }

    private void RebuildPropertyEditors()
    {
        if (_detailsContent == null)
        {
            return;
        }

        ClearDetailsContent();

        if (_selectedEntity == null)
        {
            _detailsContent.TryAddChild(new MGTextBlock(_window, "Select an entity to inspect its components.")
            {
                WrapText = true,
            });
            return;
        }

        if (_selectedComponent == null)
        {
            _detailsContent.TryAddChild(new MGTextBlock(_window, "Select a component to edit its properties.")
            {
                WrapText = true,
            });
            return;
        }

        _detailsContent.TryAddChild(new MGTextBlock(_window, $"[b]{EscapeMarkup(GetDisplayName(_selectedComponent))}[/b]")
        {
            WrapText = false,
        });
        var componentEditor = ComponentEditorRegistry.Create(_window, _selectedComponent);
        _detailsContent.TryAddChild(componentEditor.CreateView());
    }

    private void ClearDetailsContent()
    {
        if (_detailsContent == null)
        {
            return;
        }

        foreach (var child in _detailsContent.Children.ToList())
        {
            _detailsContent.TryRemoveChild(child);
        }
    }

    private void ShowAddComponentDialog()
    {
        if (_selectedEntity == null || _window.Desktop == null)
        {
            return;
        }

        var componentNames = ComponentTypes.Value.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        if (componentNames.Count == 0)
        {
            return;
        }

        const int width = 360;
        const int height = 420;
        int left = (_window.Desktop.ValidScreenBounds.Width - width) / 2;
        int top = (_window.Desktop.ValidScreenBounds.Height - height) / 2;

        var dialog = new MGWindow(_window.Desktop, left, top, width, height)
        {
            TitleText = "Add Component",
        };

        var content = new MGStackPanel(dialog, Orientation.Vertical)
        {
            Spacing = 8,
            Padding = new Thickness(8),
        };

        var listBox = new MGListBox<string>(dialog)
        {
            PreferredHeight = 320,
            ItemTemplate = item => new MGTextBlock(dialog, item)
            {
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        listBox.SetItemsSource(componentNames);
        listBox.SelectedValue = componentNames[0];

        var buttons = new MGStackPanel(dialog, Orientation.Horizontal)
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var addButton = new MGButton(dialog, _ =>
        {
            var selectedName = listBox.SelectedValue;
            if (selectedName == null || !ComponentTypes.Value.TryGetValue(selectedName, out var componentType))
            {
                return;
            }

            if (Activator.CreateInstance(componentType) is not EntityComponent component)
            {
                return;
            }

            AddComponent(component);
            dialog.TryCloseWindow();
        });
        addButton.SetContent(new MGTextBlock(dialog, "Add"));
        addButton.PreferredWidth = 80;

        var cancelButton = new MGButton(dialog, _ => dialog.TryCloseWindow());
        cancelButton.SetContent(new MGTextBlock(dialog, "Cancel"));
        cancelButton.PreferredWidth = 80;

        buttons.TryAddChild(addButton);
        buttons.TryAddChild(cancelButton);

        content.TryAddChild(new MGTextBlock(dialog, "Choose the type of component to add."));
        content.TryAddChild(listBox);
        content.TryAddChild(buttons);
        dialog.SetContent(content);
        _window.Desktop.Windows.Add(dialog);
    }

    private void AddComponent(EntityComponent component)
    {
        if (_selectedEntity == null)
        {
            return;
        }

        if (_selectedComponent is SceneComponent selectedSceneComponent && component is SceneComponent childSceneComponent)
        {
            selectedSceneComponent.AddChildComponent(childSceneComponent);
        }
        else if (component is SceneComponent sceneComponent && _selectedEntity.RootComponent == null)
        {
            _selectedEntity.RootComponent = sceneComponent;
        }
        else
        {
            _selectedEntity.AddComponent(component);
        }

        component.Initialize();
        if (_selectedEntity.World != null)
        {
            component.InitializeWithWorld(_selectedEntity.World);
        }

        _selectedComponent = component;
        RebuildComponentTree();
        SetSelectedComponent(component);
        SelectedComponentChanged?.Invoke(component);
    }

    private void UpdateSummary()
    {
        if (_componentSummaryText == null)
        {
            return;
        }

        if (_selectedEntity == null)
        {
            _componentSummaryText.SetText("No entity selected");
            return;
        }

        int componentCount = EnumerateComponents(_selectedEntity).Count();
        _componentSummaryText.SetText($"{componentCount} component{(componentCount == 1 ? string.Empty : "s")}");
    }

    private static IEnumerable<EntityComponent> EnumerateComponents(Entity entity)
    {
        if (entity.RootComponent != null)
        {
            foreach (var component in EnumerateSceneComponents(entity.RootComponent))
            {
                yield return component;
            }
        }

        foreach (var component in entity.Components)
        {
            yield return component;
            if (component is SceneComponent sceneComponent)
            {
                foreach (var child in sceneComponent.Children.SelectMany(EnumerateSceneComponents))
                {
                    yield return child;
                }
            }
        }
    }

    private static IEnumerable<SceneComponent> EnumerateSceneComponents(SceneComponent component)
    {
        yield return component;
        foreach (var child in component.Children)
        {
            foreach (var nested in EnumerateSceneComponents(child))
            {
                yield return nested;
            }
        }
    }

    private static IReadOnlyDictionary<string, Type> CreateComponentTypeLookup()
    {
        var componentType = typeof(EntityComponent);

        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericType: false }
                && type.IsSubclassOf(componentType)
                && type.GetConstructor(Type.EmptyTypes) != null)
            .Select(type => new
            {
                Type = type,
                Name = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name,
            })
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Type, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(type => type != null)!;
        }
    }

    private static string GetDisplayName(EntityComponent component)
    {
        return component.GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
            ?? component.GetType().Name;
    }

    private static string EscapeMarkup(string value)
    {
        return value.Replace("[", "[[", StringComparison.Ordinal);
    }
}