using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CasaEngine.Core.Log;
using CasaEngine.Engine;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Materials;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Controls;

public sealed class MaterialAssetInspectorPanel
{
    private readonly MGWindow _window;
    private readonly MaterialDefinitionEditorRegistry _registry;

    private MGDockPanel? _root;
    private MGTextBlock? _headerText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGStackPanel? _propertiesStack;

    private MaterialAsset? _materialAsset;
    private string? _loadedRelativePath;
    private readonly Dictionary<Guid, MaterialAsset?> _resolvedParentMaterials = new();

    public MaterialAssetInspectorPanel(MGWindow window)
        : this(window, MaterialDefinitionEditorRegistry.Default)
    {
    }

    public MaterialAssetInspectorPanel(MGWindow window, MaterialDefinitionEditorRegistry registry)
    {
        _window = window;
        _registry = registry;
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

        var scrollViewer = new MGScrollViewer(_window);
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
        _resolvedParentMaterials.Clear();
        RefreshInspector();
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

    private void RefreshInspector()
    {
        if (_propertiesStack == null || _headerText == null || _sourceText == null || _statusText == null)
        {
            return;
        }

        _propertiesStack.TryRemoveAll();

        if (_materialAsset == null)
        {
            _headerText.Text = "[b]Material Inspector[/b]";
            _sourceText.Text = "No material loaded.";
            _statusText.Text = "Open a .material asset from the Content Browser.";
            return;
        }

        _resolvedParentMaterials.Clear();

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
                _propertiesStack.TryAddChild(BuildPropertyRow(section.Properties[i]));
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

    private MGElement BuildPropertyRow(MaterialPropertyDescriptor descriptor)
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
        actions.TryAddChild(BuildPropertyStateBadge(propertyState));
        actions.TryAddChild(BuildResetButton(descriptor.Definition, propertyState.HasLocalOverride));
        header.TryAddChild(actions, Dock.Right);

        var editorRow = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            Margin = new Thickness(14, 0, 0, 0),
        };
        editorRow.TryAddChild(BuildEditor(descriptor, propertyState));

        if (!string.IsNullOrWhiteSpace(propertyState.SourceText))
        {
            editorRow.TryAddChild(new MGTextBlock(_window, EscapeMarkup(propertyState.SourceText))
            {
                FontSize = 10,
                Opacity = 0.7f,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        row.TryAddChild(header);
        row.TryAddChild(editorRow);
        return row;
    }

    private MGElement BuildEditor(MaterialPropertyDescriptor descriptor, PropertyDisplayState propertyState)
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

    private MGElement BuildPropertyStateBadge(PropertyDisplayState propertyState)
    {
        var badgeBorder = new MGBorder(_window, new Thickness(1), new MGUniformBorderBrush(new MGSolidFillBrush(propertyState.BorderColor)))
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(propertyState.BackgroundColor)),
            Padding = new Thickness(6, 2, 6, 2),
        };
        badgeBorder.SetContent(new MGTextBlock(_window, propertyState.BadgeText, propertyState.ForegroundColor, 10)
        {
            Opacity = 0.95f,
        });

        return badgeBorder;
    }

    private MGElement BuildResetButton(MaterialPropertyDefinition propertyDefinition, bool hasLocalOverride)
    {
        var button = new MGButton(_window, _ => ApplyPropertyValue(propertyDefinition, null))
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

    private MGElement BuildBooleanEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        bool isChecked = value != null && value.TryGetBoolean(out var booleanValue) && booleanValue;

        var checkBox = new MGCheckBox(_window)
        {
            IsChecked = isChecked,
        };
        checkBox.OnCheckStateChanged += (_, args) =>
        {
            ApplyPropertyValue(propertyDefinition, MaterialValue.FromBoolean(args.NewValue == true));
        };

        return checkBox;
    }

    private MGElement BuildSliderEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
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
        slider.ValueChanged += (_, args) =>
        {
            if (propertyDefinition.ValueType == MaterialPropertyType.Integer)
            {
                ApplyPropertyValue(propertyDefinition, MaterialValue.FromInteger((int)Math.Round(args.NewValue)));
                return;
            }

            ApplyPropertyValue(propertyDefinition, MaterialValue.FromFloat(args.NewValue));
        };

        return slider;
    }

    private MGElement BuildNumericEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
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
        numericField.ValueChanged += (_, numericValue) =>
        {
            if (propertyDefinition.ValueType == MaterialPropertyType.Integer)
            {
                ApplyPropertyValue(propertyDefinition, MaterialValue.FromInteger((int)Math.Round(numericValue)));
                return;
            }

            ApplyPropertyValue(propertyDefinition, MaterialValue.FromFloat(numericValue));
        };

        return numericField;
    }

    private MGElement BuildColorEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        var colorEditor = new ColorEditor(_window, value != null && value.TryGetColor(out var color) ? color : Color.White);
        colorEditor.ValueChanged += (_, selectedColor) =>
        {
            ApplyPropertyValue(propertyDefinition, MaterialValue.FromColor(selectedColor));
        };
        return colorEditor;
    }

    private MGElement BuildVector3Editor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        var vectorEditor = new Vector3Editor(_window, propertyDefinition.Step ?? 0.1f)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        if (value != null && value.TryGetVector3(out var vector3Value))
        {
            vectorEditor.Value = vector3Value;
        }

        vectorEditor.ValueChanged += (_, updatedValue) =>
        {
            ApplyPropertyValue(propertyDefinition, MaterialValue.FromVector3(updatedValue));
        };

        return vectorEditor;
    }

    private MGElement BuildTextureEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
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

        assetSelector.AssetChanged += (_, assetId) =>
        {
            ApplyPropertyValue(propertyDefinition, MaterialValue.FromTextureId(assetId));
        };

        return assetSelector;
    }

    private MGElement BuildEnumEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
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
        combo.SelectedItemChanged += (_, args) =>
        {
            if (args.NewValue != null)
            {
                ApplyPropertyValue(propertyDefinition, MaterialValue.FromEnum(args.NewValue.Value));
            }
        };

        return combo;
    }

    private MGElement BuildTextEditor(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        var textBox = new MGTextBox(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        textBox.SetText(FormatTextValue(propertyDefinition, value));
        textBox.TextChanged += (_, args) =>
        {
            if (TryParseTextValue(propertyDefinition, args.NewValue, out var parsedValue))
            {
                ApplyPropertyValue(propertyDefinition, parsedValue);
            }
        };

        return textBox;
    }

    private void ApplyPropertyValue(MaterialPropertyDefinition propertyDefinition, MaterialValue? value)
    {
        if (_materialAsset == null)
        {
            return;
        }

        try
        {
            if (value == null)
            {
                _materialAsset.RemovePropertyValue(propertyDefinition.Key);
            }
            else
            {
                _materialAsset.SetPropertyValue(propertyDefinition.Key, value);
            }

            SaveMaterialAsset();
            RefreshInspector();
            if (_statusText != null && !string.IsNullOrWhiteSpace(_loadedRelativePath))
            {
                _statusText.Text = $"Saved {EscapeMarkup(_loadedRelativePath)}";
            }
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

    private void SaveMaterialAsset()
    {
        if (_materialAsset == null || string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            return;
        }

        var document = new JObject();
        MaterialAssetJsonSerializer.Save(_materialAsset, document);
        EditorAssetWriterService.SaveDocument(_loadedRelativePath, document);

        if (_statusText != null)
        {
            _statusText.Text = $"Saved {EscapeMarkup(_loadedRelativePath)}";
        }
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
                    value = MaterialValue.FromVector2(new Microsoft.Xna.Framework.Vector2(vector2Components[0], vector2Components[1]));
                    return true;
                }

                return false;

            case MaterialPropertyType.Vector4:
                if (TryParseFloatComponents(trimmed, 4, out var vector4Components))
                {
                    value = MaterialValue.FromVector4(new Microsoft.Xna.Framework.Vector4(
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
            => new(
                effectiveValue,
                true,
                "Override",
                null,
                new Color(94, 61, 20),
                new Color(201, 145, 53),
                new Color(255, 241, 210));

        public static PropertyDisplayState Inherited(MaterialValue? effectiveValue, string sourceText)
            => new(
                effectiveValue,
                false,
                "Inherited",
                sourceText,
                new Color(33, 52, 74),
                new Color(98, 143, 188),
                new Color(223, 238, 255));

        public static PropertyDisplayState Default(MaterialValue? effectiveValue)
            => new(
                effectiveValue,
                false,
                "Default",
                null,
                new Color(42, 42, 48),
                new Color(92, 92, 104),
                new Color(226, 226, 230));
    }
}