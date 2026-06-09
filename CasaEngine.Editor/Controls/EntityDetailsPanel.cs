using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Controls.ComponentEditors;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Workspaces;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Shared.Helpers;
using MonoGame.Extended;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

public sealed class EntityDetailsPanel
{
    private static readonly EditorHistoryContext DefaultHistoryContext = new(EditorHistoryContextKind.World, EditorPanelIds.WorldViewport);

    private static readonly Lazy<IReadOnlyDictionary<string, Type>> ComponentTypes = new(CreateComponentTypeLookup);

    private readonly MGWindow _window;
    private readonly bool _includeComponentTree;
    private readonly Dictionary<MGTreeViewItem, EntityComponent> _itemToComponent = new();
    private readonly Dictionary<EntityComponent, MGTreeViewItem> _componentToItem = new();

    private MGDockPanel _root;
    private MGTextBox _entityNameTextBox;
    private MGButton _addComponentButton;
    private MGTreeView _componentTree;
    private MGScrollViewer _detailsScrollViewer;
    private MGStackPanel _detailsContent;
    private MGTextBlock _componentSummaryText;
    private World _selectedWorld;
    private Entity _selectedEntity;
    private EntityComponent _selectedComponent;
    private SceneComponent _observedSceneComponent;
    private ComponentEditorBase _activeComponentEditor;
    private bool _suppressEntityNameChanged;
    private bool _suppressComponentSelectionChanged;
    private bool _selectedComponentRefreshPending;
    private EditorHistoryContext _historyContext = DefaultHistoryContext;

    public EntityDetailsPanel(MGWindow window, bool includeComponentTree = true)
    {
        _window = window;
        _includeComponentTree = includeComponentTree;
    }

    public event Action<EntityComponent> SelectedComponentChanged;

    public EditorHistoryContext HistoryContext
    {
        get => _historyContext;
        set => _historyContext = value.IsEmpty ? DefaultHistoryContext : value;
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        var toolbar = BuildToolbar();

        if (_includeComponentTree)
        {
            _componentTree = new MGTreeView(_window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                PreferredHeight = 220,
                MinHeight = 140,
            };
            _componentTree.SelectionChanged += OnComponentTreeSelectionChanged;

            _componentSummaryText = new MGTextBlock(_window, "No entity selected")
            {
                Margin = new Thickness(6, 4, 6, 4),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        _detailsContent = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 8,
            Padding = new Thickness(6),
        };

        _detailsScrollViewer = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        _detailsScrollViewer.SetContent(_detailsContent);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(toolbar, Dock.Top);
        if (_componentSummaryText != null)
        {
            _root.TryAddChild(_componentSummaryText, Dock.Bottom);
        }

        if (_componentTree != null)
        {
            _root.TryAddChild(_componentTree, Dock.Top);
        }

        _root.TryAddChild(_detailsScrollViewer, Dock.Top);

        RefreshEntityHeader();
        RebuildComponentTree();
        RebuildPropertyEditors();
        return _root;
    }

    public void SetSelectedEntity(Entity entity)
    {
        SyncSelection(entity?.World, entity, ReferenceEquals(_selectedEntity, entity) ? _selectedComponent : null);
    }

    public void SetSelectedWorld(World world)
    {
        SyncSelection(world, null, null);
    }

    public void SetSelectedComponent(EntityComponent component)
    {
        ApplyComponentSelection(component, rebuildPropertyEditors: true);
    }

    public void SyncSelection(World world, Entity entity, EntityComponent component)
    {
        bool worldChanged = !ReferenceEquals(_selectedWorld, world);
        bool entityChanged = !ReferenceEquals(_selectedEntity, entity);
        bool componentChanged = !ReferenceEquals(_selectedComponent, component);

        if (!worldChanged && !entityChanged && !componentChanged)
        {
            return;
        }

        Trace($"SyncSelection world={DescribeWorld(world)} entity={DescribeEntity(entity)} component={DescribeComponent(component)} worldChanged={worldChanged} entityChanged={entityChanged} componentChanged={componentChanged}");

        if (entityChanged)
        {
            DetachEntity();
        }

        _selectedWorld = world;

        if (entityChanged)
        {
            _selectedEntity = entity;
            AttachEntity();
        }

        if (worldChanged || entityChanged)
        {
            RefreshEntityHeader();
            RebuildComponentTree();
        }

        ApplyComponentSelection(component, rebuildPropertyEditors: true);
    }

    public void SyncSelection(Entity entity, EntityComponent component)
    {
        SyncSelection(entity?.World, entity, component);
    }

    public void Update()
    {
        if (!_selectedComponentRefreshPending)
        {
            return;
        }

        _selectedComponentRefreshPending = false;

        if (_activeComponentEditor?.TryRefreshFromComponent() == true)
        {
            return;
        }

        if (_selectedComponent != null)
        {
            RebuildPropertyEditors();
        }
    }

    private void ApplyComponentSelection(EntityComponent component, bool rebuildPropertyEditors)
    {
        EntityComponent resolvedComponent = component;

        if (_componentTree == null)
        {
            SetSelectedComponentInternal(resolvedComponent);
            if (rebuildPropertyEditors)
            {
                RebuildPropertyEditors();
            }

            return;
        }

        _suppressComponentSelectionChanged = true;
        try
        {
            if (resolvedComponent == null)
            {
                _componentTree.ClearSelection();
            }
            else if (!_componentToItem.TryGetValue(resolvedComponent, out var item))
            {
                Trace($"ApplyComponentSelection missing tree item for component={DescribeComponent(resolvedComponent)} on entity={DescribeEntity(_selectedEntity)}");
                resolvedComponent = null;
                _componentTree.ClearSelection();
            }
            else
            {
                _componentTree.SelectItem(item);
                _componentTree.ScrollIntoView(item);
            }
        }
        finally
        {
            _suppressComponentSelectionChanged = false;
        }

        SetSelectedComponentInternal(resolvedComponent);

        if (rebuildPropertyEditors)
        {
            RebuildPropertyEditors();
        }
    }

    private MGElement BuildToolbar()
    {
        var toolbar = new MGGrid(_window)
        {
            Margin = new Thickness(4, 4, 4, 2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            ColumnSpacing = 6,
        };
        toolbar.AddRow(GridLength.Auto);
        toolbar.AddColumn(GridLength.CreatePixelLength(44));
        toolbar.AddColumn(GridLength.CreateWeightedLength(1));
        toolbar.AddColumn(GridLength.CreatePixelLength(34));

        toolbar.TryAddChild(0, 0, new MGTextBlock(_window, "Entity")
        {
            VerticalAlignment = VerticalAlignment.Center,
            PreferredWidth = 44,
        });

        _entityNameTextBox = new MGTextBox(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HasStableTextFootprint = true,
            AcceptsReturn = false,
            AcceptsTab = false,
            PreferredWidth = 180,
        };
        _entityNameTextBox.TextChanged += OnEntityNameChanged;

        _addComponentButton = new MGButton(_window, _ => ShowAddComponentDialog())
        {
            PreferredWidth = 34,
            PreferredHeight = 28,
        };
        if (EditorIcons.FilePlus != null)
        {
            _addComponentButton.SetContent(new MGImage(_window, EditorIcons.AsImage(EditorIcons.FilePlus)!, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 16,
                PreferredHeight = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        else
        {
            _addComponentButton.SetContent(new MGTextBlock(_window, "+")
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        toolbar.TryAddChild(0, 1, _entityNameTextBox);
        toolbar.TryAddChild(0, 2, _addComponentButton);
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

    private void OnSelectedEntityNameChanged(object sender, EntityNameChangedEventArgs e)
    {
        RefreshEntityHeader();
    }

    private void OnEntityComponentChanged(object sender, EntityComponent component)
    {
        Trace($"Entity component list changed entity={DescribeEntity(_selectedEntity)} changedComponent={DescribeComponent(component)}");
        RebuildComponentTree();
        RebuildPropertyEditors();
    }

    private void OnEntityNameChanged(object sender, EventArgs<string> e)
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

        var entity = _selectedEntity;
        var previousName = entity.Name;
        ExecuteHistoryCommand(
            "Rename Entity",
            () =>
            {
                entity.Name = newName;
                RefreshEntityHeader();
            },
            () =>
            {
                entity.Name = previousName;
                RefreshEntityHeader();
            });
    }

    private void RefreshEntityHeader()
    {
        if (_entityNameTextBox == null)
        {
            return;
        }

        _suppressEntityNameChanged = true;
        _entityNameTextBox.SetText(_selectedEntity?.Name ?? _selectedWorld?.Name ?? string.Empty);
        _entityNameTextBox.IsEnabled = _selectedEntity != null;
        _suppressEntityNameChanged = false;

        if (_addComponentButton != null)
        {
            _addComponentButton.IsEnabled = _selectedEntity != null;
        }
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
            ApplyComponentSelection(null, rebuildPropertyEditors: false);
            return;
        }

        if (_selectedEntity.RootComponent != null)
        {
            _componentTree.AddItem(BuildComponentTreeItem(_selectedEntity.RootComponent));
        }

        foreach (var component in _selectedEntity.Components.OrderBy(GetComponentLabel, StringComparer.OrdinalIgnoreCase))
        {
            _componentTree.AddItem(BuildComponentTreeItem(component));
        }

        UpdateSummary();
        ApplyComponentSelection(_selectedComponent, rebuildPropertyEditors: false);
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
        _componentTree?.SelectItem(item);
        _componentTree?.Focus();

        if (item.HasItems && clickCount == 1)
        {
            item.IsExpanded = !item.IsExpanded;
        }
    }

    private void OnComponentTreeSelectionChanged(object sender, MGTreeViewItem item)
    {
        if (_suppressComponentSelectionChanged)
        {
            return;
        }

        var component = item != null && _itemToComponent.TryGetValue(item, out var selectedComponent) ? selectedComponent : null;
        SetSelectedComponentInternal(component);
        Trace($"Tree selection changed entity={DescribeEntity(_selectedEntity)} component={DescribeComponent(_selectedComponent)}");
        RebuildPropertyEditors();

        SelectedComponentChanged?.Invoke(_selectedComponent);
    }

    private void RebuildPropertyEditors()
    {
        if (_detailsContent == null)
        {
            return;
        }

        Trace($"RebuildPropertyEditors entity={DescribeEntity(_selectedEntity)} component={DescribeComponent(_selectedComponent)}");

        using (_detailsContent.SuspendContentLayout())
        {
            ClearDetailsContent();

            if (_selectedEntity == null)
            {
                _activeComponentEditor = null;
                if (_selectedWorld != null)
                {
                    BuildWorldPropertyEditors();
                }
                else
                {
                    _detailsContent.TryAddChild(new MGTextBlock(_window, "Select an entity or the world to inspect its properties.")
                    {
                        WrapText = true,
                    });
                }

                return;
            }

            if (_selectedComponent == null)
            {
                _activeComponentEditor = null;
                BuildEntityPropertyEditors();
                return;
            }

            _detailsContent.TryAddChild(new MGTextBlock(_window, $"[b]{EscapeMarkup(GetComponentLabel(_selectedComponent))}[/b]")
            {
                WrapText = false,
            });
            _activeComponentEditor = ComponentEditorRegistry.Create(_window, _selectedComponent, RefreshSelectedComponentEditor, HistoryContext);
            _detailsContent.TryAddChild(_activeComponentEditor.CreateView());
        }
    }

    private void RefreshSelectedComponentEditor()
    {
        RebuildComponentTree();
        RebuildPropertyEditors();
    }

    private void SetSelectedComponentInternal(EntityComponent component)
    {
        if (ReferenceEquals(_selectedComponent, component))
        {
            return;
        }

        DetachSelectedComponentObservers();
        _selectedComponent = component;
        _activeComponentEditor = null;
        AttachSelectedComponentObservers();
    }

    private void AttachSelectedComponentObservers()
    {
        if (_selectedComponent is not SceneComponent sceneComponent)
        {
            return;
        }

        _observedSceneComponent = sceneComponent;
        sceneComponent.LocalTransform.PositionChanged += OnSelectedSceneComponentTransformChanged;
        sceneComponent.LocalTransform.OrientationChanged += OnSelectedSceneComponentTransformChanged;
        sceneComponent.LocalTransform.ScaleChanged += OnSelectedSceneComponentTransformChanged;
    }

    private void DetachSelectedComponentObservers()
    {
        if (_observedSceneComponent == null)
        {
            return;
        }

        _observedSceneComponent.LocalTransform.PositionChanged -= OnSelectedSceneComponentTransformChanged;
        _observedSceneComponent.LocalTransform.OrientationChanged -= OnSelectedSceneComponentTransformChanged;
        _observedSceneComponent.LocalTransform.ScaleChanged -= OnSelectedSceneComponentTransformChanged;
        _observedSceneComponent = null;
    }

    private void OnSelectedSceneComponentTransformChanged(object sender, EventArgs e)
    {
        _selectedComponentRefreshPending = true;
    }

    private void BuildWorldPropertyEditors()
    {
        if (_detailsContent == null || _selectedWorld == null)
        {
            return;
        }

        _detailsContent.TryAddChild(new MGTextBlock(_window, $"[b]{EscapeMarkup(string.IsNullOrWhiteSpace(_selectedWorld.Name) ? "World" : _selectedWorld.Name)}[/b]")
        {
            WrapText = false,
        });
        _detailsContent.TryAddChild(new MGTextBlock(_window, "Edit world-level environment lighting and per-scene directional shadow-map settings. Ambient tint is linear RGB, intensities are non-negative multipliers, and the shadow controls drive the forward V1 shadow pass for this world.")
        {
            WrapText = true,
        });

        var settings = _selectedWorld.EnvironmentSettings;
        var grid = CreatePropertyGrid();
        int rowIndex = 0;

        var backgroundModeCombo = CreateStringCombo(Enum.GetNames<EnvironmentBackgroundMode>(), settings.BackgroundMode.ToString(), value =>
        {
            ApplyWorldEnvironmentChange(
                "Change Environment Background Mode",
                static s => s.BackgroundMode,
                static (s, selectedMode) => s.BackgroundMode = selectedMode,
                Enum.Parse<EnvironmentBackgroundMode>(value));
        });
        rowIndex = AddPropertyRow(grid, rowIndex, "Background Mode", backgroundModeCombo);

        var backgroundColorEditor = new ColorEditor(_window, settings.BackgroundColor);
        backgroundColorEditor.ValueChanged += (_, value) => ApplyWorldEnvironmentChange(
            "Change Environment Background Color",
            static s => s.BackgroundColor,
            static (s, color) => s.BackgroundColor = color,
            value);
        rowIndex = AddPropertyRow(grid, rowIndex, "Background Color", backgroundColorEditor);

        var environmentAssetSelector = new AssetSelector(_window)
        {
            AssetId = settings.EnvironmentAssetId,
            Filter = static assetInfo => string.Equals(assetInfo.AssetType, "environment", StringComparison.OrdinalIgnoreCase),
        };
        environmentAssetSelector.AssetChanged += (_, value) => ApplyWorldEnvironmentChange(
            "Change Environment Asset",
            static s => s.EnvironmentAssetId,
            static (s, assetId) => s.EnvironmentAssetId = assetId,
            value);
        rowIndex = AddPropertyRow(grid, rowIndex, "Environment Asset", environmentAssetSelector);

        var backgroundCubemapSelector = new AssetSelector(_window)
        {
            AssetId = settings.BackgroundCubemapAssetId,
            Filter = static assetInfo => string.Equals(assetInfo.AssetType, "dds", StringComparison.OrdinalIgnoreCase),
        };
        backgroundCubemapSelector.AssetChanged += (_, value) => ApplyWorldEnvironmentChange(
            "Change Environment Background Cubemap",
            static s => s.BackgroundCubemapAssetId,
            static (s, assetId) => s.BackgroundCubemapAssetId = assetId,
            value);
        rowIndex = AddPropertyRow(grid, rowIndex, "Background Cubemap", backgroundCubemapSelector);

        var reflectionCubemapSelector = new AssetSelector(_window)
        {
            AssetId = settings.SpecularEnvironmentCubemapAssetId,
            Filter = static assetInfo => string.Equals(assetInfo.AssetType, "dds", StringComparison.OrdinalIgnoreCase),
        };
        reflectionCubemapSelector.AssetChanged += (_, value) => ApplyWorldEnvironmentChange(
            "Change Environment Reflection Cubemap",
            static s => s.SpecularEnvironmentCubemapAssetId,
            static (s, assetId) => s.SpecularEnvironmentCubemapAssetId = assetId,
            value);
        rowIndex = AddPropertyRow(grid, rowIndex, "Reflection Cubemap", reflectionCubemapSelector);

        var ambientColorEditor = new Vector3ColorEditor(_window, settings.AmbientColor);
        ambientColorEditor.ValueChanged += (_, value) => ApplyWorldEnvironmentChange(
            "Change Environment Ambient Tint",
            static s => s.AmbientColor,
            static (s, color) => s.AmbientColor = color,
            value);
        rowIndex = AddPropertyRow(grid, rowIndex, "Ambient Tint", ambientColorEditor);

        var ambientIntensityEditor = new NumericField(_window, min: 0.0f, step: 0.05f)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Value = settings.AmbientIntensity,
        };
        ambientIntensityEditor.ValueChanged += (_, value) => ApplyWorldEnvironmentChange(
            "Change Environment Ambient Intensity",
            static s => s.AmbientIntensity,
            static (s, intensity) => s.AmbientIntensity = intensity,
            value);
        rowIndex = AddPropertyRow(grid, rowIndex, "Ambient Intensity", ambientIntensityEditor);

        var specularIntensityEditor = new NumericField(_window, min: 0.0f, step: 0.05f)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Value = settings.SpecularIntensity,
        };
        specularIntensityEditor.ValueChanged += (_, value) => ApplyWorldEnvironmentChange(
            "Change Environment Specular Intensity",
            static s => s.SpecularIntensity,
            static (s, intensity) => s.SpecularIntensity = intensity,
            value);
        rowIndex = AddPropertyRow(grid, rowIndex, "Specular Intensity", specularIntensityEditor);

        _detailsContent.TryAddChild(grid);

        _detailsContent.TryAddChild(new MGTextBlock(_window, "[b]Directional Shadows (V1)[/b]")
        {
            WrapText = false,
        });
        _detailsContent.TryAddChild(new MGTextBlock(_window, "These controls are serialized with the world and drive the shared forward directional shadow map for static and skinned receivers.")
        {
            WrapText = true,
        });

        var shadowGrid = CreatePropertyGrid();
        rowIndex = 0;

        var shadowsEnabledCheckBox = new MGCheckBox(_window)
        {
            IsChecked = settings.Shadows.Enabled,
        };
        shadowsEnabledCheckBox.OnCheckStateChanged += (_, e) => ApplyWorldEnvironmentChange(
            "Toggle World Shadow Maps",
            static s => s.Shadows.Enabled,
            static (s, enabled) => s.Shadows.Enabled = enabled,
            e.NewValue ?? false);
        rowIndex = AddPropertyRow(shadowGrid, rowIndex, "Enabled", shadowsEnabledCheckBox);

        var shadowResolutionItems = new List<string> { "256", "512", "1024", "2048", "4096" };
        string selectedShadowResolution = settings.Shadows.Resolution.ToString();
        if (!shadowResolutionItems.Contains(selectedShadowResolution, StringComparer.Ordinal))
        {
            shadowResolutionItems.Add(selectedShadowResolution);
            shadowResolutionItems.Sort(static (left, right) => int.Parse(left).CompareTo(int.Parse(right)));
        }

        var shadowResolutionCombo = CreateStringCombo(shadowResolutionItems, selectedShadowResolution, value =>
        {
            ApplyWorldEnvironmentChange(
                "Change Shadow Map Resolution",
                static s => s.Shadows.Resolution,
                static (s, resolution) => s.Shadows.Resolution = resolution,
                int.Parse(value));
        });
        rowIndex = AddPropertyRow(shadowGrid, rowIndex, "Resolution", shadowResolutionCombo);

        var shadowDepthBiasEditor = new NumericField(_window, min: 0.0f, step: 0.0005f)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Value = settings.Shadows.DepthBias,
        };
        shadowDepthBiasEditor.ValueChanged += (_, value) => ApplyWorldEnvironmentChange(
            "Change Shadow Depth Bias",
            static s => s.Shadows.DepthBias,
            static (s, bias) => s.Shadows.DepthBias = bias,
            value);
        rowIndex = AddPropertyRow(shadowGrid, rowIndex, "Depth Bias", shadowDepthBiasEditor);

        var shadowNormalBiasEditor = new NumericField(_window, min: 0.0f, step: 0.001f)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Value = settings.Shadows.NormalBias,
        };
        shadowNormalBiasEditor.ValueChanged += (_, value) => ApplyWorldEnvironmentChange(
            "Change Shadow Normal Bias",
            static s => s.Shadows.NormalBias,
            static (s, bias) => s.Shadows.NormalBias = bias,
            value);
        rowIndex = AddPropertyRow(shadowGrid, rowIndex, "Normal Bias", shadowNormalBiasEditor);

        var shadowMaxDistanceEditor = new NumericField(_window, min: 0.0f, step: 1.0f)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Value = settings.Shadows.MaxDistance,
        };
        shadowMaxDistanceEditor.ValueChanged += (_, value) => ApplyWorldEnvironmentChange(
            "Change Shadow Max Distance",
            static s => s.Shadows.MaxDistance,
            static (s, distance) => s.Shadows.MaxDistance = distance,
            value);
        rowIndex = AddPropertyRow(shadowGrid, rowIndex, "Max Distance", shadowMaxDistanceEditor);

        _detailsContent.TryAddChild(shadowGrid);

        var rebuildButton = new MGButton(_window, _ =>
        {
            settings.MarkDirty();
            RebuildPropertyEditors();
        });
        rebuildButton.SetContent(new MGTextBlock(_window, "Rebuild Environment"));
        rebuildButton.PreferredWidth = 180;
        _detailsContent.TryAddChild(rebuildButton);
    }

    private void BuildEntityPropertyEditors()
    {
        if (_detailsContent == null || _selectedEntity == null)
        {
            return;
        }

        _detailsContent.TryAddChild(new MGTextBlock(_window, $"[b]{EscapeMarkup(string.IsNullOrWhiteSpace(_selectedEntity.Name) ? "Entity" : _selectedEntity.Name)}[/b]")
        {
            WrapText = false,
        });
        _detailsContent.TryAddChild(new MGTextBlock(_window, "Edit entity-level policies. Engine defaults are derived from attached components until you switch the source to Explicit.")
        {
            WrapText = true,
        });

        var grid = CreatePropertyGrid();
        int rowIndex = 0;

        var sourceCombo = CreateStringCombo(Enum.GetNames<EntityPolicySourceMode>(), _selectedEntity.Policies.PolicySourceMode.ToString(), value =>
        {
            ApplyEntityPolicyChange(
                "Change Entity Policy Source",
            static entity => entity.Policies.PolicySourceMode,
            static (entity, sourceMode) => entity.Policies.PolicySourceMode = sourceMode,
                Enum.Parse<EntityPolicySourceMode>(value));
        });
        rowIndex = AddPropertyRow(grid, rowIndex, "Policy Source", sourceCombo);

        bool explicitPolicies = _selectedEntity.Policies.PolicySourceMode == EntityPolicySourceMode.Explicit;

        var mobilityCombo = CreateStringCombo(Enum.GetNames<Mobility>(), _selectedEntity.Policies.Mobility.ToString(), value =>
        {
            ApplyEntityPolicyChange(
                "Change Entity Mobility",
            static entity => entity.Policies.Mobility,
            static (entity, mobility) => entity.Policies.Mobility = mobility,
                Enum.Parse<Mobility>(value));
        });
        mobilityCombo.IsEnabled = explicitPolicies;
        rowIndex = AddPropertyRow(grid, rowIndex, "Mobility", mobilityCombo);

        var tickCombo = CreateStringCombo(Enum.GetNames<TickPolicy>(), _selectedEntity.Policies.TickPolicy.ToString(), value =>
        {
            ApplyEntityPolicyChange(
                "Change Entity Tick Policy",
            static entity => entity.Policies.TickPolicy,
            static (entity, tickPolicy) => entity.Policies.TickPolicy = tickPolicy,
                Enum.Parse<TickPolicy>(value));
        });
        tickCombo.IsEnabled = explicitPolicies;
        rowIndex = AddPropertyRow(grid, rowIndex, "Tick Policy", tickCombo);

        var spatialCombo = CreateStringCombo(Enum.GetNames<SpatialPolicy>(), _selectedEntity.Policies.SpatialPolicy.ToString(), value =>
        {
            ApplyEntityPolicyChange(
                "Change Entity Spatial Policy",
            static entity => entity.Policies.SpatialPolicy,
            static (entity, spatialPolicy) => entity.Policies.SpatialPolicy = spatialPolicy,
                Enum.Parse<SpatialPolicy>(value));
        });
        spatialCombo.IsEnabled = explicitPolicies;
        rowIndex = AddPropertyRow(grid, rowIndex, "Spatial Policy", spatialCombo);

        var renderCombo = CreateStringCombo(Enum.GetNames<RenderDynamicPolicy>(), _selectedEntity.Policies.RenderDynamicPolicy.ToString(), value =>
        {
            ApplyEntityPolicyChange(
                "Change Entity Render Policy",
            static entity => entity.Policies.RenderDynamicPolicy,
            static (entity, renderPolicy) => entity.Policies.RenderDynamicPolicy = renderPolicy,
                Enum.Parse<RenderDynamicPolicy>(value));
        });
        renderCombo.IsEnabled = explicitPolicies;
        rowIndex = AddPropertyRow(grid, rowIndex, "Render Policy", renderCombo);

        _detailsContent.TryAddChild(grid);

        ResolvedEntityPolicies resolvedPolicies = EntityPolicyResolver.ResolveRuntimePolicies(_selectedEntity);
        _detailsContent.TryAddChild(new MGTextBlock(
            _window,
            $"Effective runtime policy: {resolvedPolicies.PolicySet.Mobility} / {resolvedPolicies.PolicySet.TickPolicy} / {resolvedPolicies.PolicySet.SpatialPolicy} / {resolvedPolicies.PolicySet.RenderDynamicPolicy} ({resolvedPolicies.SourceMode}).")
        {
            WrapText = true,
        });
    }

    private void ClearDetailsContent()
    {
        if (_detailsContent == null)
        {
            return;
        }

        _detailsContent.TryRemoveAll();
    }

    private void ApplyWorldEnvironmentChange<T>(
        string description,
        Func<WorldEnvironmentSettings, T> getter,
        Action<WorldEnvironmentSettings, T> setter,
        T newValue)
    {
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);

        if (_selectedWorld == null)
        {
            return;
        }

        var settings = _selectedWorld.EnvironmentSettings;
        var currentValue = getter(settings);
        if (EqualityComparer<T>.Default.Equals(currentValue, newValue))
        {
            return;
        }

        ExecuteHistoryCommand(
            description,
            () =>
            {
                setter(settings, newValue);
                settings.MarkDirty();
                RebuildPropertyEditors();
            },
            () =>
            {
                setter(settings, currentValue);
                settings.MarkDirty();
                RebuildPropertyEditors();
            });
    }

    private void ApplyEntityPolicyChange<T>(
        string description,
        Func<Entity, T> getter,
        Action<Entity, T> setter,
        T newValue)
    {
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);

        if (_selectedEntity == null)
        {
            return;
        }

        Entity entity = _selectedEntity;
        T currentValue = getter(entity);
        if (EqualityComparer<T>.Default.Equals(currentValue, newValue))
        {
            return;
        }

        ExecuteHistoryCommand(
            description,
            () =>
            {
                setter(entity, newValue);
                RebuildPropertyEditors();
            },
            () =>
            {
                setter(entity, currentValue);
                RebuildPropertyEditors();
            });
    }

    private MGGrid CreatePropertyGrid()
    {
        var grid = new MGGrid(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ColumnSpacing = 8,
            RowSpacing = 6,
        };

        grid.AddColumn(GridLength.CreatePixelLength(160));
        grid.AddColumn(GridLength.CreateWeightedLength(1));
        return grid;
    }

    private int AddPropertyRow(MGGrid grid, int rowIndex, string label, MGElement editor)
    {
        grid.AddRow(GridLength.Auto);
        grid.TryAddChild(rowIndex, 0, new MGTextBlock(_window, label)
        {
            VerticalAlignment = VerticalAlignment.Center,
        });

        editor.HorizontalAlignment = HorizontalAlignment.Stretch;
        grid.TryAddChild(rowIndex, 1, editor);
        return rowIndex + 1;
    }

    private MGComboBox<string> CreateStringCombo(IEnumerable<string> items, string selectedItem, Action<string> onChanged)
    {
        var combo = new MGComboBox<string>(_window)
        {
            MinWidth = 140,
        };

        combo.DropdownItemTemplate = item =>
        {
            var button = combo.CreateDefaultDropdownButton();
            button.SetContent(item);
            return button;
        };

        combo.SelectedItemTemplate = item => new MGTextBlock(_window, item)
        {
            Padding = new Thickness(4, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var itemList = items.ToList();
        combo.SetItemsSource(itemList);
        combo.SelectedItem = selectedItem ?? itemList.FirstOrDefault();
        combo.SelectedItemChanged += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.NewValue))
            {
                onChanged(e.NewValue);
            }
        };
        return combo;
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
        var dialog = EditorModalDialogHelper.CreateCenteredModalWindow(_window, width, height, "Add Component");

        var content = new MGGrid(dialog)
        {
            Padding = new Thickness(8),
            RowSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        content.AddColumn(GridLength.CreateWeightedLength(1));
        content.AddRow(GridLength.Auto);
        content.AddRow(GridLength.CreateWeightedLength(1));
        content.AddRow(GridLength.Auto);

        var listBox = new MGListBox<string>(dialog)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
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

        content.TryAddChild(0, 0, new MGTextBlock(dialog, "Choose the type of component to add."));
        content.TryAddChild(1, 0, listBox);
        content.TryAddChild(2, 0, buttons);
        dialog.SetContent(content);
    }

    private void AddComponent(EntityComponent component)
    {
        if (_selectedEntity == null)
        {
            return;
        }

        var entity = _selectedEntity;
        var previousComponent = _selectedComponent;
        var selectedSceneComponent = _selectedComponent as SceneComponent;
        bool attachAsChild = selectedSceneComponent != null && component is SceneComponent;
        bool attachAsRoot = !attachAsChild && component is SceneComponent && entity.RootComponent == null;

        ExecuteHistoryCommand(
            "Add Component",
            () =>
            {
                AttachComponent(entity, component, selectedSceneComponent, attachAsChild, attachAsRoot);
                ApplyComponentMutationSelection(component);
            },
            () =>
            {
                DetachComponent(entity, component, selectedSceneComponent, attachAsChild, attachAsRoot);
                ApplyComponentMutationSelection(previousComponent);
            });
    }

    private void ExecuteHistoryCommand(string description, Action execute, Action undo)
    {
        EditorHistoryService.Current.Execute(
            HistoryContext.IsEmpty ? DefaultHistoryContext : HistoryContext,
            new EditorDelegateCommand(description, execute, undo));
    }

    private void ApplyComponentMutationSelection(EntityComponent component)
    {
        _selectedComponent = component;
        RebuildComponentTree();
        SetSelectedComponent(component);
        SelectedComponentChanged?.Invoke(component);
    }

    private static void AttachComponent(Entity entity, EntityComponent component, SceneComponent selectedSceneComponent, bool attachAsChild, bool attachAsRoot)
    {
        if (attachAsChild && selectedSceneComponent != null && component is SceneComponent childSceneComponent)
        {
            selectedSceneComponent.AddChildComponent(childSceneComponent);
        }
        else if (attachAsRoot && component is SceneComponent rootSceneComponent)
        {
            entity.RootComponent = rootSceneComponent;
        }
        else
        {
            entity.AddComponent(component);
        }

        component.Initialize();
        if (entity.World != null)
        {
            component.InitializeWithWorld(entity.World);
        }
    }

    private static void DetachComponent(Entity entity, EntityComponent component, SceneComponent selectedSceneComponent, bool attachAsChild, bool attachAsRoot)
    {
        if (attachAsChild && selectedSceneComponent != null && component is SceneComponent childSceneComponent)
        {
            selectedSceneComponent.RemoveChildComponent(childSceneComponent);
            return;
        }

        if (attachAsRoot && component is SceneComponent rootSceneComponent && ReferenceEquals(entity.RootComponent, rootSceneComponent))
        {
            entity.RootComponent = null;
            return;
        }

        entity.RemoveComponent(component);
    }

    private void UpdateSummary()
    {
        if (_componentSummaryText == null)
        {
            return;
        }

        if (_selectedEntity == null)
        {
            _componentSummaryText.SetText(_selectedWorld != null ? "World settings" : "No entity selected");
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
                && type.GetConstructor(Type.EmptyTypes) != null
                && IsAddableComponentType(type))
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

    private static bool IsAddableComponentType(Type type)
    {
        return type.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false;
    }

    private static string GetDisplayName(EntityComponent component)
    {
        return component.GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
            ?? component.GetType().Name;
    }

    private static string GetComponentLabel(EntityComponent component)
    {
        var displayName = GetDisplayName(component);
        var instanceName = component.Name?.Trim();

        if (string.IsNullOrWhiteSpace(instanceName)
            || string.Equals(instanceName, displayName, StringComparison.OrdinalIgnoreCase)
            || instanceName.StartsWith("Object ", StringComparison.OrdinalIgnoreCase))
        {
            return displayName;
        }

        return $"{displayName} [{instanceName}]";
    }

    private static string EscapeMarkup(string value)
    {
        return value.Replace("[", "[[", StringComparison.Ordinal);
    }

    private static string DescribeEntity(Entity entity)
    {
        return entity == null
            ? "<null>"
            : $"'{entity.Name}'";
    }

    private static string DescribeWorld(World world)
    {
        return world == null
            ? "<null>"
            : $"'{world.Name}'";
    }

    private static string DescribeComponent(EntityComponent component)
    {
        if (component == null)
        {
            return "<null>";
        }

        return $"'{GetComponentLabel(component)}' owner={DescribeEntity(component.Owner)}";
    }

    private static void Trace(string message)
    {
        Logs.WriteTrace($"[EntityDetails] {message}");
    }
}