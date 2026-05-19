using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Styling;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.History;
using CasaEngine.EditorServices.Materials;
using CasaEngine.Framework.Assets;

using CasaEngine.Framework.Scene.World;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Controls;

public sealed class MaterialAssetInspectorPanel : IDisposable
{
    private readonly MGWindow _window;
    private readonly MaterialDefinitionEditorRegistry _registry;
    private readonly MaterialPreviewViewport? _materialPreview;

    private MGDockPanel? _root;
    private MGElement _previewContent;
    private MGTextBlock? _headerText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGStackPanel? _propertiesStack;

    private MaterialAsset? _materialAsset;
    private string? _loadedRelativePath;
    private string? _historyContextId;
    private string? _savedSnapshot;
    private bool _isDirty;
    private readonly Dictionary<Guid, MaterialAsset?> _resolvedParentMaterials = new();
    private readonly Dictionary<string, PropertyRowBinding> _propertyRows = new(StringComparer.OrdinalIgnoreCase);

    public MaterialAssetInspectorPanel(MGWindow window)
        : this(window, MaterialDefinitionEditorRegistry.Default, null, null)
    {
    }

    internal MaterialAssetInspectorPanel(MGWindow window, HostedEditorGameAdapter editorRuntime, GraphicsDevice graphicsDevice)
        : this(window, MaterialDefinitionEditorRegistry.Default, editorRuntime, graphicsDevice)
    {
    }

    public MaterialAssetInspectorPanel(MGWindow window, MaterialDefinitionEditorRegistry registry)
        : this(window, registry, null, null)
    {
    }

    internal MaterialAssetInspectorPanel(
        MGWindow window,
        MaterialDefinitionEditorRegistry registry,
        HostedEditorGameAdapter? editorRuntime,
        GraphicsDevice? graphicsDevice)
    {
        _window = window;
        _registry = registry;
        if (editorRuntime != null && graphicsDevice != null)
        {
            _materialPreview = new MaterialPreviewViewport(window, graphicsDevice, editorRuntime);
        }
    }

    public string? LoadedRelativePath => _loadedRelativePath;

    public MaterialAsset? LoadedMaterialAsset => _materialAsset;

    public bool IsDirty => _isDirty;

    public event Action<MaterialAssetInspectorPanel>? DirtyStateChanged;

    public MGElement CreatePreviewContent()
    {
        if (_materialPreview != null)
        {
            return _materialPreview.CreateContent();
        }

        if (_previewContent != null)
        {
            return _previewContent;
        }

        _previewContent = new MGTextBlock(_window, "Material preview unavailable.")
        {
            Margin = new Thickness(8, 6, 8, 4),
            Opacity = 0.75f,
            WrapText = true,
        };
        return _previewContent;
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _headerText = new MGTextBlock(_window, "[b]Material Inspector[/b]")
        {
            Margin = new Thickness(8, 6, 8, 4),
            WrapText = true,
        };

        _sourceText = new MGTextBlock(_window, "No material loaded.")
        {
            Margin = new Thickness(8, 0, 8, 4),
            Opacity = 0.8f,
            WrapText = true,
        };

        _statusText = new MGTextBlock(_window, "Open a .material asset from the Content Browser.")
        {
            Margin = new Thickness(8, 0, 8, 8),
            Opacity = 0.7f,
            WrapText = true,
        };

        _propertiesStack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 4,
            Margin = new Thickness(4),
        };

        var scrollViewer = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        scrollViewer.SetContent(_propertiesStack);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(_headerText, Dock.Top);
        _root.TryAddChild(_sourceText, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Top);
        _root.TryAddChild(scrollViewer, Dock.Top);

        RefreshInspector();
        return _root;
    }

    public void LoadAsset(MaterialAsset materialAsset, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        _materialAsset = materialAsset;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        _savedSnapshot = SerializeMaterialAsset(materialAsset);
        _resolvedParentMaterials.Clear();
        _materialPreview?.SetMaterialAsset(materialAsset);
        SetDirty(false);
        RefreshInspector();
    }

    public void SetHistoryContextId(string historyContextId)
    {
        _historyContextId = historyContextId;
    }

    public IReadOnlyList<string> GetAutomationPropertyStateSnapshot()
    {
        if (_materialAsset == null)
        {
            return Array.Empty<string>();
        }

        _resolvedParentMaterials.Clear();

        var definition = _materialAsset.GetRequiredDefinition();
        var result = new List<string>();
        foreach (var section in _registry.GetSections(definition.Id))
        {
            for (int i = 0; i < section.Properties.Count; i++)
            {
                var descriptor = section.Properties[i];
                var propertyState = ResolvePropertyState(descriptor.Definition);
                string line = $"{descriptor.Definition.Key}: {propertyState.BadgeText}";
                if (!string.IsNullOrWhiteSpace(propertyState.SourceText))
                {
                    line += $" ({propertyState.SourceText})";
                }

                result.Add(line);
            }
        }

        return result;
    }

    public IReadOnlyList<string> GetAutomationPreviewStateSnapshot()
    {
        return _materialPreview?.GetAutomationStateSnapshot() ?? Array.Empty<string>();
    }

    public World? GetOrCreatePreviewWorld()
    {
        return _materialPreview?.GetOrCreatePreviewWorld();
    }

    public void RefreshPreviewAfterDraw()
    {
        _materialPreview?.RefreshAfterDraw();
    }

    public void Dispose()
    {
        _materialPreview?.Dispose();
    }

    public bool ReloadFromDisk()
    {
        if (string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            return false;
        }

        if (IsDirty)
        {
            if (_statusText != null)
            {
                _statusText.Text = $"Unsaved changes kept for {EscapeMarkup(_loadedRelativePath)}";
            }

            return false;
        }

        string fullPath = Path.Combine(EngineEnvironment.ProjectPath, _loadedRelativePath);
        if (!TryLoadMaterialAsset(fullPath, out var materialAsset))
        {
            return false;
        }

        LoadAsset(materialAsset, fullPath);
        return true;
    }

    public bool TryApplyAutomationPropertyOverrideAndSave(string propertyKey, string rawValue, out string statusMessage)
    {
        statusMessage = string.Empty;

        if (_materialAsset == null)
        {
            statusMessage = "No material loaded.";
            return false;
        }

        if (!TryFindPropertyDefinition(propertyKey, out var propertyDefinition))
        {
            statusMessage = $"Property '{propertyKey}' not found.";
            return false;
        }

        if (!TryCreateAutomationValue(propertyDefinition, rawValue, out var value))
        {
            statusMessage = $"Unable to parse '{rawValue}' for property '{propertyDefinition.Key}'.";
            return false;
        }

        try
        {
            _materialAsset.SetPropertyValue(propertyDefinition.Key, value);
            UpdateDirtyStateFromCurrentMaterial();
            if (!TrySaveLoadedAsset(out string? saveError))
            {
                statusMessage = saveError ?? "Unable to save material asset.";
                return false;
            }

            RefreshEditedProperty(propertyDefinition, refreshEditorValue: true);
            statusMessage = string.IsNullOrWhiteSpace(_loadedRelativePath)
                ? $"Saved property '{propertyDefinition.Key}'."
                : $"Saved {EscapeMarkup(_loadedRelativePath)} ({propertyDefinition.Key}={rawValue})";
            return true;
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            statusMessage = exception.Message;
            return false;
        }
    }

    private void RefreshInspector()
    {
        if (_propertiesStack == null || _headerText == null || _sourceText == null || _statusText == null)
        {
            return;
        }

        _propertyRows.Clear();
        _propertiesStack.TryRemoveAll();

        if (_materialAsset == null)
        {
            _headerText.Text = "[b]Material Inspector[/b]";
            _sourceText.Text = "No material loaded.";
            _statusText.Text = "Open a .material asset from the Content Browser.";
            _materialPreview?.SetMaterialAsset(null);
            return;
        }

        _resolvedParentMaterials.Clear();
        _materialPreview?.SetMaterialAsset(_materialAsset);

        var definition = _materialAsset.GetRequiredDefinition();
        _headerText.Text = $"[b]{EscapeMarkup(_materialAsset.Name)}[/b]";
        _sourceText.Text = BuildSourceText(definition);
        _statusText.Text = string.IsNullOrWhiteSpace(_loadedRelativePath)
            ? string.Empty
            : $"Asset: {EscapeMarkup(_loadedRelativePath)}";

        foreach (var section in _registry.GetSections(definition.Id))
        {
            _propertiesStack.TryAddChild(BuildSectionHeader(section.DisplayName));

            for (int i = 0; i < section.Properties.Count; i++)
            {
                var propertyRow = BuildPropertyRow(section.Properties[i]);
                _propertiesStack.TryAddChild(propertyRow.Root);
                _propertyRows[propertyRow.Descriptor.Definition.Key] = propertyRow;
            }
        }
    }

    private MGElement BuildSectionHeader(string title)
    {
        return new MGTextBlock(_window, $"[b]{EscapeMarkup(title)}[/b]")
        {
            Margin = new Thickness(4, 8, 4, 2),
            Opacity = 0.85f,
        };
    }

    private PropertyRowBinding BuildPropertyRow(MaterialPropertyDescriptor descriptor)
    {
        var propertyState = ResolvePropertyState(descriptor.Definition);

        var row = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 2,
            Margin = new Thickness(2, 2, 2, 4),
        };

        var header = new MGDockPanel(_window);
        header.TryAddChild(new MGTextBlock(_window, EscapeMarkup(descriptor.DisplayName))
        {
            Margin = new Thickness(2, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        }, Dock.Left);

        var actions = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
        };

        var badgeText = new MGTextBlock(_window, propertyState.BadgeText, propertyState.ForegroundColor, 10)
        {
            Opacity = 0.95f,
        };
        var badgeBorder = new MGBorder(
            _window,
            new Thickness(1),
            new MGUniformBorderBrush(new MGSolidFillBrush(propertyState.BorderColor)))
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(propertyState.BackgroundColor)),
            Padding = new Thickness(6, 2, 6, 2),
        };
        badgeBorder.SetContent(badgeText);

        var resetButton = BuildResetButton(descriptor.Definition, propertyState.HasLocalOverride);

        actions.TryAddChild(badgeBorder);
        actions.TryAddChild(resetButton);
        header.TryAddChild(actions, Dock.Right);

        var editorRow = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            Margin = new Thickness(14, 0, 0, 0),
        };

        var editorBinding = BuildEditorBinding(descriptor, propertyState);
        editorRow.TryAddChild(editorBinding.Element);

        var sourceText = new MGTextBlock(_window, string.Empty)
        {
            FontSize = 10,
            Opacity = 0.7f,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        if (!string.IsNullOrWhiteSpace(propertyState.SourceText))
        {
            sourceText.Text = EscapeMarkup(propertyState.SourceText);
            sourceText.Visibility = Visibility.Visible;
        }

        editorRow.TryAddChild(sourceText);

        row.TryAddChild(header);
        row.TryAddChild(editorRow);
        return new PropertyRowBinding(descriptor, row, badgeBorder, badgeText, resetButton, sourceText, editorBinding);
    }

    private IPropertyEditorBinding BuildEditorBinding(MaterialPropertyDescriptor descriptor, PropertyDisplayState propertyState)
    {
        var propertyDefinition = descriptor.Definition;
        var value = propertyState.EffectiveValue;

        return propertyDefinition.ValueType switch
        {
            MaterialPropertyType.Boolean => BuildBooleanEditor(propertyDefinition, value),
            MaterialPropertyType.Float or MaterialPropertyType.Integer when string.Equals(descriptor.EditorControlHint, "Slider", StringComparison.Ordinal)
                => BuildSliderEditor(propertyDefinition, value),
            MaterialPropertyType.Float or MaterialPropertyType.Integer => BuildNumericEditor(propertyDefinition, value),
            MaterialPropertyType.Color => BuildColorEditor(propertyDefinition, value),
            MaterialPropertyType.Texture => BuildTextureEditor(propertyDefinition, value),
            MaterialPropertyType.Enum => BuildEnumEditor(propertyDefinition, value),
            MaterialPropertyType.Vector3 when string.Equals(descriptor.EditorControlHint, "ColorPicker", StringComparison.Ordinal)
                => BuildVector3ColorEditor(propertyDefinition, value),
            MaterialPropertyType.Vector3 => BuildVector3Editor(propertyDefinition, value),
            _ => BuildTextEditor(propertyDefinition, value),
        };
    }

    private string BuildSourceText(MaterialDefinition definition)
    {
        string sourceText = $"Definition: {EscapeMarkup(definition.DisplayName)}";
        if (_materialAsset == null || _materialAsset.ParentMaterialAssetId == Guid.Empty)
        {
            return sourceText;
        }

        var parentMaterial = ResolveMaterialAsset(_materialAsset.ParentMaterialAssetId);
        if (parentMaterial == null)
        {
            return sourceText + " | Parent: unresolved";
        }

        return sourceText + $" | Parent: {EscapeMarkup(parentMaterial.Name)}";
    }

    private PropertyDisplayState ResolvePropertyState(MaterialPropertyDefinition propertyDefinition)
    {
        if (_materialAsset == null)
        {
            return PropertyDisplayState.Default(propertyDefinition.GetDefaultMaterialValue());
        }

        bool hasLocalOverride = _materialAsset.HasLocalPropertyValue(propertyDefinition.Key);
        var effectiveValue = _materialAsset.GetPropertyValueOrDefault(propertyDefinition.Key, ResolveMaterialAsset);
        if (hasLocalOverride)
        {
            return PropertyDisplayState.Local(effectiveValue);
        }

        if (TryGetInheritedPropertySource(propertyDefinition.Key, out var sourceAsset, out _))
        {
            return PropertyDisplayState.Inherited(effectiveValue, $"From {sourceAsset.Name}");
        }

        return PropertyDisplayState.Default(effectiveValue);
    }

    private bool TryGetInheritedPropertySource(string propertyKey, out MaterialAsset sourceAsset, out MaterialValue value)
    {
        sourceAsset = null!;
        value = null!;

        if (_materialAsset == null || _materialAsset.ParentMaterialAssetId == Guid.Empty)
        {
            return false;
        }

        var visitedAssetIds = new HashSet<Guid> { _materialAsset.Id };
        Guid currentParentId = _materialAsset.ParentMaterialAssetId;

        while (currentParentId != Guid.Empty && visitedAssetIds.Add(currentParentId))
        {
            var parentMaterial = ResolveMaterialAsset(currentParentId);
            if (parentMaterial == null)
            {
                return false;
            }

            if (parentMaterial.TryGetPropertyValue(propertyKey, out value))
            {
                sourceAsset = parentMaterial;
                return true;
            }

            currentParentId = parentMaterial.ParentMaterialAssetId;
        }

        return false;
    }

    private MaterialAsset? ResolveMaterialAsset(Guid assetId)
    {
        if (assetId == Guid.Empty)
        {
            return null;
        }

        if (_resolvedParentMaterials.TryGetValue(assetId, out var cachedMaterial))
        {
            return cachedMaterial;
        }

        var assetInfo = AssetCatalog.Get(assetId);
        if (assetInfo == null || string.IsNullOrWhiteSpace(assetInfo.FileName))
        {
            _resolvedParentMaterials[assetId] = null;
            return null;
        }

        string relativePath = assetInfo.FileName.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(EngineEnvironment.ProjectPath, relativePath);
        if (!File.Exists(fullPath))
        {
            _resolvedParentMaterials[assetId] = null;
            return null;
        }

        try
        {
            var document = JObject.Parse(File.ReadAllText(fullPath));
            if (document["definition_id"] == null && document["type"] == null)
            {
                _resolvedParentMaterials[assetId] = null;
                return null;
            }

            var materialAsset = new MaterialAsset();
            materialAsset.Load(document);
            materialAsset.Name = assetInfo.Name;
            materialAsset.AssetId = assetInfo.Id;
            materialAsset.FileName = assetInfo.FileName;

            _resolvedParentMaterials[assetId] = materialAsset;
            return materialAsset;
        }
        catch
        {
            _resolvedParentMaterials[assetId] = null;
            return null;
        }
    }

    private MGButton BuildResetButton(MaterialPropertyDefinition propertyDefinition, bool hasLocalOverride)
    {
        var button = new MGButton(_window, _ => ApplyPropertyValue(propertyDefinition, null, refreshEditorValue: true))
        {
            IsEnabled = hasLocalOverride,
            PreferredWidth = 54,
        };
        button.SetContent(new MGTextBlock(_window, "Reset")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });

        return button;
    }

    private IPropertyEditorBinding BuildBooleanEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        bool isChecked = value != null && value.TryGetBoolean(out var booleanValue) && booleanValue;

        var checkBox = new MGCheckBox(_window)
        {
            IsChecked = isChecked,
        };

        return new BooleanEditorBinding(checkBox, newValue => ApplyPropertyValue(propertyDefinition, newValue));
    }

    private IPropertyEditorBinding BuildSliderEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        float minimum = propertyDefinition.MinValue ?? 0.0f;
        float maximum = propertyDefinition.MaxValue ?? 1.0f;
        float currentValue = GetNumericValue(propertyDefinition, value, minimum);

        var slider = new MGSlider(_window, minimum, maximum, currentValue)
        {
            MinWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ShowValueLabel = true,
            ValueLabelFormat = propertyDefinition.ValueType == MaterialPropertyType.Integer ? "F0" : "F2",
        };

        return new SliderEditorBinding(slider, propertyDefinition, newValue => ApplyPropertyValue(propertyDefinition, newValue));
    }

    private IPropertyEditorBinding BuildNumericEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        var numericField = new NumericField(
            _window,
            min: propertyDefinition.MinValue ?? float.MinValue,
            max: propertyDefinition.MaxValue ?? float.MaxValue,
            step: propertyDefinition.Step ?? 1.0f)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        numericField.Value = GetNumericValue(propertyDefinition, value, 0.0f);

        return new NumericEditorBinding(numericField, propertyDefinition, newValue => ApplyPropertyValue(propertyDefinition, newValue));
    }

    private IPropertyEditorBinding BuildColorEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        var colorEditor = new ColorEditor(_window, value != null && value.TryGetColor(out var color) ? color : Color.White);

        return new ColorEditorBinding(colorEditor, newValue => ApplyPropertyValue(propertyDefinition, newValue));
    }

    private IPropertyEditorBinding BuildVector3ColorEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        var colorEditor = new Vector3ColorEditor(_window, value != null && value.TryGetVector3(out var vector3Value) ? vector3Value : Vector3.Zero)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        return new Vector3ColorEditorBinding(colorEditor, newValue => ApplyPropertyValue(propertyDefinition, newValue));
    }

    private IPropertyEditorBinding BuildVector3Editor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        var vectorEditor = new Vector3Editor(_window, propertyDefinition.Step ?? 0.1f)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        if (value != null && value.TryGetVector3(out var vector3Value))
        {
            vectorEditor.Value = vector3Value;
        }

        return new Vector3EditorBinding(vectorEditor, newValue => ApplyPropertyValue(propertyDefinition, newValue));
    }

    private IPropertyEditorBinding BuildTextureEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        var assetSelector = new AssetSelector(_window)
        {
            AssetId = value != null && value.TryGetTextureId(out var textureAssetId) ? textureAssetId : Guid.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        if (!string.IsNullOrWhiteSpace(propertyDefinition.AssetKind))
        {
            assetSelector.Filter = assetInfo => string.Equals(assetInfo.AssetType, propertyDefinition.AssetKind, StringComparison.OrdinalIgnoreCase);
        }

        return new TextureEditorBinding(assetSelector, newValue => ApplyPropertyValue(propertyDefinition, newValue));
    }

    private IPropertyEditorBinding BuildEnumEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        var combo = new MGComboBox<MaterialPropertyOption>(_window)
        {
            MinWidth = 160,
        };
        combo.DropdownItemTemplate = item =>
        {
            var button = combo.CreateDefaultDropdownButton();
            button.SetContent(item.DisplayName);
            return button;
        };
        combo.SelectedItemTemplate = item => new MGTextBlock(_window, item.DisplayName)
        {
            Padding = new Thickness(4, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var options = new List<MaterialPropertyOption>(propertyDefinition.Options.Count);
        for (int index = 0; index < propertyDefinition.Options.Count; index++)
        {
            options.Add(propertyDefinition.Options[index]);
        }

        combo.SetItemsSource(options);

        string? currentValue = value != null && value.TryGetEnum(out var enumValue) ? enumValue : null;
        combo.SelectedItem = GetSelectedOption(options, currentValue);

        return new EnumEditorBinding(combo, options, newValue => ApplyPropertyValue(propertyDefinition, newValue));
    }

    private IPropertyEditorBinding BuildTextEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        var textBox = new MGTextBox(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        textBox.SetText(FormatTextValue(propertyDefinition, value));

        return new TextEditorBinding(textBox, propertyDefinition, newValue => ApplyPropertyValue(propertyDefinition, newValue));
    }

    private void ApplyPropertyValue(MaterialPropertyDefinition propertyDefinition, MaterialValue? value, bool refreshEditorValue = false)
    {
        if (_materialAsset == null)
        {
            return;
        }

        bool hadLocalOverride = _materialAsset.TryGetPropertyValue(propertyDefinition.Key, out var previousValue);
        bool hasNextLocalOverride = value != null;
        if (hadLocalOverride == hasNextLocalOverride && Equals(previousValue, value))
        {
            return;
        }

        try
        {
            ExecuteMaterialPropertyCommand(propertyDefinition, hadLocalOverride, previousValue, hasNextLocalOverride, value, refreshEditorValue);
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            if (_statusText != null)
            {
                _statusText.Text = $"Failed to update '{propertyDefinition.DisplayName}': {exception.Message}";
            }
        }
    }

    private void RefreshEditedProperty(MaterialPropertyDefinition propertyDefinition, bool refreshEditorValue)
    {
        _resolvedParentMaterials.Clear();
        RefreshPropertyRow(propertyDefinition, refreshEditorValue);
        _materialPreview?.RefreshMaterialAsset();
    }

    private void RefreshPropertyRow(MaterialPropertyDefinition propertyDefinition, bool refreshEditorValue)
    {
        if (!_propertyRows.TryGetValue(propertyDefinition.Key, out var propertyRow))
        {
            return;
        }

        var propertyState = ResolvePropertyState(propertyDefinition);
        propertyRow.ApplyState(propertyState);
        if (refreshEditorValue)
        {
            propertyRow.EditorBinding.UpdateValue(propertyState.EffectiveValue);
        }
    }

    public bool TrySaveLoadedAsset(out string? errorMessage)
    {
        errorMessage = null;

        if (_materialAsset == null || string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            errorMessage = "No material is loaded.";
            return false;
        }

        if (!IsDirty)
        {
            if (_statusText != null)
            {
                _statusText.Text = $"Already saved {EscapeMarkup(_loadedRelativePath)}";
            }

            return true;
        }

        try
        {
            var document = new JObject();
            MaterialAssetJsonSerializer.Save(_materialAsset, document);
            EditorAssetWriterService.SaveDocument(_loadedRelativePath, document, EditorAssetSaveSource.MaterialInspectorPanel);
            _savedSnapshot = document.ToString(Formatting.None);
            SetDirty(false);

            if (_statusText != null)
            {
                _statusText.Text = $"Saved {EscapeMarkup(_loadedRelativePath)}";
            }

            return true;
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            errorMessage = exception.Message;
            if (_statusText != null)
            {
                _statusText.Text = $"Failed to save {EscapeMarkup(_loadedRelativePath)}: {exception.Message}";
            }

            return false;
        }
    }

    private void UpdateDirtyStateFromCurrentMaterial()
    {
        if (_materialAsset == null)
        {
            SetDirty(false);
            return;
        }

        SetDirty(!string.Equals(SerializeMaterialAsset(_materialAsset), _savedSnapshot, StringComparison.Ordinal));
    }

    private void ExecuteMaterialPropertyCommand(
        MaterialPropertyDefinition propertyDefinition,
        bool hadPreviousLocalOverride,
        MaterialValue? previousValue,
        bool hasNextLocalOverride,
        MaterialValue? nextValue,
        bool refreshEditorValue)
    {
        if (TryGetHistoryContext(out var historyContext))
        {
            EditorHistoryService.Current.Execute(
                historyContext,
                new EditorDelegateCommand(
                    BuildMaterialCommandDescription(propertyDefinition, hasNextLocalOverride),
                    () => ApplyLocalPropertyState(propertyDefinition, hasNextLocalOverride, nextValue, refreshEditorValue: true),
                    () => ApplyLocalPropertyState(propertyDefinition, hadPreviousLocalOverride, previousValue, refreshEditorValue: true)));
            return;
        }

        ApplyLocalPropertyState(propertyDefinition, hasNextLocalOverride, nextValue, refreshEditorValue);
    }

    private void ApplyLocalPropertyState(
        MaterialPropertyDefinition propertyDefinition,
        bool hasLocalOverride,
        MaterialValue? value,
        bool refreshEditorValue)
    {
        if (_materialAsset == null)
        {
            return;
        }

        if (hasLocalOverride && value != null)
        {
            _materialAsset.SetPropertyValue(propertyDefinition.Key, value);
        }
        else
        {
            _materialAsset.RemovePropertyValue(propertyDefinition.Key);
        }

        UpdateDirtyStateFromCurrentMaterial();
        RefreshEditedProperty(propertyDefinition, refreshEditorValue);
        if (_statusText != null && !string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            _statusText.Text = IsDirty
                ? $"Modified {EscapeMarkup(_loadedRelativePath)}"
                : $"Asset: {EscapeMarkup(_loadedRelativePath)}";
        }
    }

    private bool TryGetHistoryContext(out EditorHistoryContext historyContext)
    {
        if (string.IsNullOrWhiteSpace(_historyContextId))
        {
            historyContext = EditorHistoryContext.Empty;
            return false;
        }

        historyContext = new EditorHistoryContext(EditorHistoryContextKind.Material, _historyContextId);
        return true;
    }

    private static string BuildMaterialCommandDescription(MaterialPropertyDefinition propertyDefinition, bool hasLocalOverride)
        => hasLocalOverride
            ? $"Set {propertyDefinition.DisplayName}"
            : $"Reset {propertyDefinition.DisplayName}";

    private void SetDirty(bool isDirty)
    {
        if (_isDirty == isDirty)
        {
            return;
        }

        _isDirty = isDirty;
        DirtyStateChanged?.Invoke(this);
    }

    private static string SerializeMaterialAsset(MaterialAsset materialAsset)
    {
        var document = new JObject();
        MaterialAssetJsonSerializer.Save(materialAsset, document);
        return document.ToString(Formatting.None);
    }

    private bool TryFindPropertyDefinition(string propertyKey, out MaterialPropertyDefinition propertyDefinition)
    {
        propertyDefinition = null!;
        if (_materialAsset == null)
        {
            return false;
        }

        var definition = _materialAsset.GetRequiredDefinition();
        for (int i = 0; i < definition.Properties.Count; i++)
        {
            if (!string.Equals(definition.Properties[i].Key, propertyKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            propertyDefinition = definition.Properties[i];
            return true;
        }

        return false;
    }

    private static bool TryCreateAutomationValue(MaterialPropertyDefinition propertyDefinition, string rawValue, out MaterialValue value)
    {
        value = null!;
        string trimmed = rawValue.Trim();

        switch (propertyDefinition.ValueType)
        {
            case MaterialPropertyType.Boolean:
                if (bool.TryParse(trimmed, out bool boolValue))
                {
                    value = MaterialValue.FromBoolean(boolValue);
                    return true;
                }

                break;

            case MaterialPropertyType.Float:
                if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                {
                    value = MaterialValue.FromFloat(floatValue);
                    return true;
                }

                break;

            case MaterialPropertyType.Integer:
                if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                {
                    value = MaterialValue.FromInteger(intValue);
                    return true;
                }

                break;

            case MaterialPropertyType.Color:
                if (TryParseColor(trimmed, out Color colorValue))
                {
                    value = MaterialValue.FromColor(colorValue);
                    return true;
                }

                break;

            case MaterialPropertyType.Texture:
                if (Guid.TryParse(trimmed, out Guid textureAssetId))
                {
                    value = MaterialValue.FromTextureId(textureAssetId);
                    return true;
                }

                break;

            case MaterialPropertyType.Vector3:
                if (TryParseFloatComponents(trimmed, 3, out var vector3Components))
                {
                    value = MaterialValue.FromVector3(new Vector3(vector3Components[0], vector3Components[1], vector3Components[2]));
                    return true;
                }

                break;
        }

        return TryParseTextValue(propertyDefinition, trimmed, out value);
    }

    private static bool TryLoadMaterialAsset(string fullPath, out MaterialAsset materialAsset)
    {
        materialAsset = new MaterialAsset();

        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            var document = JObject.Parse(File.ReadAllText(fullPath));
            if (document["definition_id"] == null && document["type"] == null)
            {
                return false;
            }

            materialAsset.Load(document);
            materialAsset.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);

            var assetInfo = AssetCatalog.GetByFileName(materialAsset.FileName)
                ?? AssetCatalog.GetByFileName(materialAsset.FileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (assetInfo != null)
            {
                materialAsset.Name = assetInfo.Name;
                materialAsset.AssetId = assetInfo.Id;
                materialAsset.FileName = assetInfo.FileName;
            }
            else
            {
                materialAsset.AssetId = materialAsset.Id;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseColor(string value, out Color color)
    {
        color = Color.White;

        string[] tokens = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length is not 3 and not 4)
        {
            return false;
        }

        byte[] channels = new byte[4] { 255, 255, 255, 255 };
        for (int i = 0; i < tokens.Length; i++)
        {
            if (!byte.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out channels[i]))
            {
                return false;
            }
        }

        color = new Color(channels[0], channels[1], channels[2], channels[3]);
        return true;
    }

    private static float GetNumericValue(MaterialPropertyDefinition propertyDefinition, MaterialValue? value, float fallback)
    {
        if (value == null)
        {
            return fallback;
        }

        if (propertyDefinition.ValueType == MaterialPropertyType.Integer && value.TryGetInteger(out var integerValue))
        {
            return integerValue;
        }

        if (propertyDefinition.ValueType == MaterialPropertyType.Float && value.TryGetFloat(out var floatValue))
        {
            return floatValue;
        }

        return fallback;
    }

    private static MaterialPropertyOption? GetSelectedOption(IReadOnlyList<MaterialPropertyOption> options, string? currentValue)
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].Value, currentValue, StringComparison.OrdinalIgnoreCase))
            {
                return options[i];
            }
        }

        return options.Count > 0 ? options[0] : null;
    }

    private static string FormatTextValue(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (propertyDefinition.ValueType == MaterialPropertyType.String && value.TryGetString(out var stringValue))
        {
            return stringValue ?? string.Empty;
        }

        if (propertyDefinition.ValueType == MaterialPropertyType.Enum && value.TryGetEnum(out var enumValue))
        {
            return enumValue ?? string.Empty;
        }

        if (propertyDefinition.ValueType == MaterialPropertyType.Vector2 && value.TryGetVector2(out var vector2Value))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{vector2Value.X}, {vector2Value.Y}");
        }

        if (propertyDefinition.ValueType == MaterialPropertyType.Vector4 && value.TryGetVector4(out var vector4Value))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{vector4Value.X}, {vector4Value.Y}, {vector4Value.Z}, {vector4Value.W}");
        }

        return string.Empty;
    }

    private static bool TryParseTextValue(MaterialPropertyDefinition propertyDefinition, string? rawValue, out MaterialValue? value)
    {
        value = null;
        string trimmed = rawValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return true;
        }

        switch (propertyDefinition.ValueType)
        {
            case MaterialPropertyType.String:
                value = MaterialValue.FromString(trimmed);
                return true;

            case MaterialPropertyType.Enum:
                value = MaterialValue.FromEnum(trimmed);
                return true;

            case MaterialPropertyType.Vector2:
                if (TryParseFloatComponents(trimmed, 2, out var vector2Components))
                {
                    value = MaterialValue.FromVector2(new Vector2(vector2Components[0], vector2Components[1]));
                    return true;
                }

                return false;

            case MaterialPropertyType.Vector4:
                if (TryParseFloatComponents(trimmed, 4, out var vector4Components))
                {
                    value = MaterialValue.FromVector4(new Vector4(
                        vector4Components[0],
                        vector4Components[1],
                        vector4Components[2],
                        vector4Components[3]));
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    private static bool TryParseFloatComponents(string value, int expectedCount, out float[] components)
    {
        var tokens = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != expectedCount)
        {
            components = Array.Empty<float>();
            return false;
        }

        components = new float[expectedCount];
        for (int i = 0; i < tokens.Length; i++)
        {
            if (!float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out components[i]))
            {
                components = Array.Empty<float>();
                return false;
            }
        }

        return true;
    }

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");

    private interface IPropertyEditorBinding
    {
        MGElement Element { get; }

        void UpdateValue(MaterialValue? value);
    }

    private sealed class PropertyRowBinding
    {
        public PropertyRowBinding(
            MaterialPropertyDescriptor descriptor,
            MGElement root,
            MGBorder badgeBorder,
            MGTextBlock badgeText,
            MGButton resetButton,
            MGTextBlock sourceText,
            IPropertyEditorBinding editorBinding)
        {
            Descriptor = descriptor;
            Root = root;
            BadgeBorder = badgeBorder;
            BadgeText = badgeText;
            ResetButton = resetButton;
            SourceText = sourceText;
            EditorBinding = editorBinding;
        }

        public MaterialPropertyDescriptor Descriptor { get; }

        public MGElement Root { get; }

        public MGBorder BadgeBorder { get; }

        public MGTextBlock BadgeText { get; }

        public MGButton ResetButton { get; }

        public MGTextBlock SourceText { get; }

        public IPropertyEditorBinding EditorBinding { get; }

        public void ApplyState(PropertyDisplayState propertyState)
        {
            BadgeBorder.BorderBrush = new MGUniformBorderBrush(new MGSolidFillBrush(propertyState.BorderColor));
            BadgeBorder.BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(propertyState.BackgroundColor));
            BadgeText.Text = propertyState.BadgeText;
            BadgeText.Foreground = new VisualStateSetting<Color?>(propertyState.ForegroundColor, propertyState.ForegroundColor, propertyState.ForegroundColor);
            ResetButton.IsEnabled = propertyState.HasLocalOverride;

            if (string.IsNullOrWhiteSpace(propertyState.SourceText))
            {
                SourceText.Text = string.Empty;
                SourceText.Visibility = Visibility.Collapsed;
                return;
            }

            SourceText.Text = EscapeMarkup(propertyState.SourceText);
            SourceText.Visibility = Visibility.Visible;
        }
    }

    private sealed class BooleanEditorBinding : IPropertyEditorBinding
    {
        private readonly MGCheckBox _checkBox;
        private readonly Action<MaterialValue?> _applyValue;
        private bool _isUpdating;

        public BooleanEditorBinding(MGCheckBox checkBox, Action<MaterialValue?> applyValue)
        {
            _checkBox = checkBox;
            _applyValue = applyValue;
            _checkBox.OnCheckStateChanged += (_, args) =>
            {
                if (_isUpdating)
                {
                    return;
                }

                _applyValue(MaterialValue.FromBoolean(args.NewValue == true));
            };
        }

        public MGElement Element => _checkBox;

        public void UpdateValue(MaterialValue? value)
        {
            bool isChecked = value != null && value.TryGetBoolean(out var booleanValue) && booleanValue;
            _isUpdating = true;
            _checkBox.IsChecked = isChecked;
            _isUpdating = false;
        }
    }

    private sealed class SliderEditorBinding : IPropertyEditorBinding
    {
        private readonly MGSlider _slider;
        private readonly MaterialPropertyDefinition _propertyDefinition;
        private readonly Action<MaterialValue?> _applyValue;
        private bool _isUpdating;

        public SliderEditorBinding(MGSlider slider, MaterialPropertyDefinition propertyDefinition, Action<MaterialValue?> applyValue)
        {
            _slider = slider;
            _propertyDefinition = propertyDefinition;
            _applyValue = applyValue;
            _slider.ValueChanged += (_, args) =>
            {
                if (_isUpdating)
                {
                    return;
                }

                if (_propertyDefinition.ValueType == MaterialPropertyType.Integer)
                {
                    _applyValue(MaterialValue.FromInteger((int)Math.Round(args.NewValue)));
                    return;
                }

                _applyValue(MaterialValue.FromFloat(args.NewValue));
            };
        }

        public MGElement Element => _slider;

        public void UpdateValue(MaterialValue? value)
        {
            float fallback = _propertyDefinition.MinValue ?? 0.0f;
            _isUpdating = true;
            _slider.Value = GetNumericValue(_propertyDefinition, value, fallback);
            _isUpdating = false;
        }
    }

    private sealed class NumericEditorBinding : IPropertyEditorBinding
    {
        private readonly NumericField _numericField;
        private readonly MaterialPropertyDefinition _propertyDefinition;
        private readonly Action<MaterialValue?> _applyValue;
        private bool _isUpdating;

        public NumericEditorBinding(NumericField numericField, MaterialPropertyDefinition propertyDefinition, Action<MaterialValue?> applyValue)
        {
            _numericField = numericField;
            _propertyDefinition = propertyDefinition;
            _applyValue = applyValue;
            _numericField.ValueChanged += (_, numericValue) =>
            {
                if (_isUpdating)
                {
                    return;
                }

                if (_propertyDefinition.ValueType == MaterialPropertyType.Integer)
                {
                    _applyValue(MaterialValue.FromInteger((int)Math.Round(numericValue)));
                    return;
                }

                _applyValue(MaterialValue.FromFloat(numericValue));
            };
        }

        public MGElement Element => _numericField;

        public void UpdateValue(MaterialValue? value)
        {
            _isUpdating = true;
            _numericField.Value = GetNumericValue(_propertyDefinition, value, 0.0f);
            _isUpdating = false;
        }
    }

    private sealed class ColorEditorBinding : IPropertyEditorBinding
    {
        private readonly ColorEditor _colorEditor;
        private readonly Action<MaterialValue?> _applyValue;
        private bool _isUpdating;

        public ColorEditorBinding(ColorEditor colorEditor, Action<MaterialValue?> applyValue)
        {
            _colorEditor = colorEditor;
            _applyValue = applyValue;
            _colorEditor.ValueChanged += (_, selectedColor) =>
            {
                if (_isUpdating)
                {
                    return;
                }

                _applyValue(MaterialValue.FromColor(selectedColor));
            };
        }

        public MGElement Element => _colorEditor;

        public void UpdateValue(MaterialValue? value)
        {
            _isUpdating = true;
            _colorEditor.Value = value != null && value.TryGetColor(out var color) ? color : Color.White;
            _isUpdating = false;
        }
    }

    private sealed class Vector3EditorBinding : IPropertyEditorBinding
    {
        private readonly Vector3Editor _vectorEditor;
        private readonly Action<MaterialValue?> _applyValue;
        private bool _isUpdating;

        public Vector3EditorBinding(Vector3Editor vectorEditor, Action<MaterialValue?> applyValue)
        {
            _vectorEditor = vectorEditor;
            _applyValue = applyValue;
            _vectorEditor.ValueChanged += (_, updatedValue) =>
            {
                if (_isUpdating)
                {
                    return;
                }

                _applyValue(MaterialValue.FromVector3(updatedValue));
            };
        }

        public MGElement Element => _vectorEditor;

        public void UpdateValue(MaterialValue? value)
        {
            _isUpdating = true;
            _vectorEditor.Value = value != null && value.TryGetVector3(out var vector3Value)
                ? vector3Value
                : Vector3.Zero;
            _isUpdating = false;
        }
    }

    private sealed class Vector3ColorEditorBinding : IPropertyEditorBinding
    {
        private readonly Vector3ColorEditor _colorEditor;
        private readonly Action<MaterialValue?> _applyValue;
        private bool _isUpdating;

        public Vector3ColorEditorBinding(Vector3ColorEditor colorEditor, Action<MaterialValue?> applyValue)
        {
            _colorEditor = colorEditor;
            _applyValue = applyValue;
            _colorEditor.ValueChanged += (_, updatedValue) =>
            {
                if (_isUpdating)
                {
                    return;
                }

                _applyValue(MaterialValue.FromVector3(updatedValue));
            };
        }

        public MGElement Element => _colorEditor;

        public void UpdateValue(MaterialValue? value)
        {
            _isUpdating = true;
            _colorEditor.Value = value != null && value.TryGetVector3(out var vector3Value)
                ? vector3Value
                : Vector3.Zero;
            _isUpdating = false;
        }
    }

    private sealed class TextureEditorBinding : IPropertyEditorBinding
    {
        private readonly AssetSelector _assetSelector;
        private readonly Action<MaterialValue?> _applyValue;

        public TextureEditorBinding(AssetSelector assetSelector, Action<MaterialValue?> applyValue)
        {
            _assetSelector = assetSelector;
            _applyValue = applyValue;
            _assetSelector.AssetChanged += (_, assetId) => _applyValue(MaterialValue.FromTextureId(assetId));
        }

        public MGElement Element => _assetSelector;

        public void UpdateValue(MaterialValue? value)
        {
            _assetSelector.AssetId = value != null && value.TryGetTextureId(out var textureAssetId)
                ? textureAssetId
                : Guid.Empty;
        }
    }

    private sealed class EnumEditorBinding : IPropertyEditorBinding
    {
        private readonly MGComboBox<MaterialPropertyOption> _combo;
        private readonly IReadOnlyList<MaterialPropertyOption> _options;
        private readonly Action<MaterialValue?> _applyValue;
        private bool _isUpdating;

        public EnumEditorBinding(
            MGComboBox<MaterialPropertyOption> combo,
            IReadOnlyList<MaterialPropertyOption> options,
            Action<MaterialValue?> applyValue)
        {
            _combo = combo;
            _options = options;
            _applyValue = applyValue;
            _combo.SelectedItemChanged += (_, args) =>
            {
                if (_isUpdating || args.NewValue == null)
                {
                    return;
                }

                _applyValue(MaterialValue.FromEnum(args.NewValue.Value));
            };
        }

        public MGElement Element => _combo;

        public void UpdateValue(MaterialValue? value)
        {
            string? currentValue = value != null && value.TryGetEnum(out var enumValue) ? enumValue : null;
            _isUpdating = true;
            _combo.SelectedItem = GetSelectedOption(_options, currentValue);
            _isUpdating = false;
        }
    }

    private sealed class TextEditorBinding : IPropertyEditorBinding
    {
        private readonly MGTextBox _textBox;
        private readonly MaterialPropertyDefinition _propertyDefinition;
        private readonly Action<MaterialValue?> _applyValue;
        private bool _isUpdating;

        public TextEditorBinding(MGTextBox textBox, MaterialPropertyDefinition propertyDefinition, Action<MaterialValue?> applyValue)
        {
            _textBox = textBox;
            _propertyDefinition = propertyDefinition;
            _applyValue = applyValue;
            _textBox.TextChanged += (_, args) =>
            {
                if (_isUpdating)
                {
                    return;
                }

                if (TryParseTextValue(_propertyDefinition, args.NewValue, out var parsedValue))
                {
                    _applyValue(parsedValue);
                }
            };
        }

        public MGElement Element => _textBox;

        public void UpdateValue(MaterialValue? value)
        {
            _isUpdating = true;
            _textBox.SetText(FormatTextValue(_propertyDefinition, value));
            _isUpdating = false;
        }
    }

    private readonly record struct PropertyDisplayState(
        MaterialValue? EffectiveValue,
        bool HasLocalOverride,
        string BadgeText,
        string? SourceText,
        Color BackgroundColor,
        Color BorderColor,
        Color ForegroundColor)
    {
        public static PropertyDisplayState Local(MaterialValue? effectiveValue)
            => Create(effectiveValue, true, "Override", null, EditorThemePalette.OverrideBadge);

        public static PropertyDisplayState Inherited(MaterialValue? effectiveValue, string sourceText)
            => Create(effectiveValue, false, "Inherited", sourceText, EditorThemePalette.InheritedBadge);

        public static PropertyDisplayState Default(MaterialValue? effectiveValue)
            => Create(effectiveValue, false, "Default", null, EditorThemePalette.DefaultBadge);

        private static PropertyDisplayState Create(
            MaterialValue? effectiveValue,
            bool hasLocalOverride,
            string badgeText,
            string? sourceText,
            EditorBadgeColors colors)
            => new(
                effectiveValue,
                hasLocalOverride,
                badgeText,
                sourceText,
                colors.BackgroundColor,
                colors.BorderColor,
                colors.ForegroundColor);
    }
}