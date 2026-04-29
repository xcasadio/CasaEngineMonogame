using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Workspaces;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities.Components;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace CasaEngine.Editor.Controls.ComponentEditors;

public abstract class ComponentEditorBase
{
    private static readonly EditorHistoryContext DefaultHistoryContext = new(EditorHistoryContextKind.World, EditorPanelIds.WorldViewport);

    protected static readonly HashSet<string> DefaultUnsupportedPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(EntityComponent.Owner),
        nameof(SceneComponent.Children),
        nameof(SceneComponent.Parent),
        nameof(SceneComponent.Forward),
        nameof(SceneComponent.Up),
        nameof(SceneComponent.BoundingBox),
        nameof(SceneComponent.WorldMatrixWithScale),
        nameof(SceneComponent.WorldMatrixNoParentScale),
        nameof(SceneComponent.WorldMatrixNoScale),
        nameof(SceneComponent.WorldInvertTransposeMatrix),
        nameof(PhysicsBaseComponent.PhysicsDefinition),
        nameof(StaticModelComponent.StaticModel),
        nameof(PhysicsBaseComponent.Collisions),
        nameof(EntityComponent.Name),
    };

    protected MGWindow Window { get; }
    protected EntityComponent Component { get; }
    protected Action? RefreshRequested { get; }
    public EditorHistoryContext HistoryContext { get; set; } = DefaultHistoryContext;

    protected ComponentEditorBase(MGWindow window, EntityComponent component, Action? refreshRequested = null)
    {
        Window = window;
        Component = component;
        RefreshRequested = refreshRequested;
    }

    public MGElement CreateView()
    {
        var root = new MGStackPanel(Window, Orientation.Vertical)
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        BuildEditor(root);
        return root;
    }

    public virtual bool TryRefreshFromComponent()
    {
        return false;
    }

    protected abstract void BuildEditor(MGStackPanel root);

    protected MGTextBlock CreateMessage(string text)
    {
        return new MGTextBlock(Window, text)
        {
            WrapText = true,
        };
    }

    protected MGTextBlock CreateReadOnlyValue(string text)
    {
        return new MGTextBlock(Window, string.IsNullOrWhiteSpace(text) ? "<none>" : text)
        {
            WrapText = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    protected MGExpander CreateSection(string headerText, bool isExpanded = true)
    {
        var expander = new MGExpander(Window)
        {
            IsExpanded = isExpanded,
        };

        var expanderButtonBackground = expander.ExpanderButtonBackgroundBrush?.Copy() ?? new VisualStateFillBrush(SolidFillBrushes.Transparent);
        expanderButtonBackground.SetAll(SolidFillBrushes.Transparent);
        expander.ExpanderButtonBackgroundBrush = expanderButtonBackground;
        expander.ExpanderButtonBorderBrush = MGUniformBorderBrush.Transparent;
        expander.ExpanderButtonBorderThickness = new Thickness(0);
        expander.ExpanderToggleButton.Padding = new Thickness(0);

        expander.Header = new MGTextBlock(Window, headerText)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        return expander;
    }

    protected MGGrid CreatePropertyGrid()
    {
        var grid = new MGGrid(Window)
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

    protected int AddPropertyRow(MGGrid grid, int rowIndex, string label, MGElement editor)
    {
        grid.AddRow(GridLength.Auto);

        grid.TryAddChild(rowIndex, 0, new MGTextBlock(Window, label)
        {
            VerticalAlignment = VerticalAlignment.Center,
        });

        editor.HorizontalAlignment = HorizontalAlignment.Stretch;
        grid.TryAddChild(rowIndex, 1, editor);
        return rowIndex + 1;
    }

    protected MGExpander? CreateGenericSection(
        object target,
        string headerText,
        ISet<string>? excludedPropertyNames = null,
        Func<PropertyDescriptor, bool>? includeProperty = null)
    {
        var grid = CreatePropertyGrid();
        int rowIndex = 0;

        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(target))
        {
            if (!CanEditProperty(property, excludedPropertyNames, includeProperty))
            {
                continue;
            }

            var editor = CreatePropertyEditor(target, property);
            if (editor == null)
            {
                continue;
            }

            rowIndex = AddPropertyRow(grid, rowIndex, property.DisplayName, editor);
        }

        if (rowIndex == 0)
        {
            return null;
        }

        var section = CreateSection(headerText);
        section.SetContent(grid);
        return section;
    }

    protected MGComboBox<string> CreateStringCombo(IEnumerable<string> items, string? selectedItem, Action<string> onChanged)
    {
        var combo = new MGComboBox<string>(Window)
        {
            MinWidth = 140,
        };

        combo.DropdownItemTemplate = item =>
        {
            var button = combo.CreateDefaultDropdownButton();
            button.SetContent(item);
            return button;
        };

        combo.SelectedItemTemplate = item => new MGTextBlock(Window, item)
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

    protected static string GetDisplayName(EntityComponent component)
    {
        return component.GetType().GetCustomAttributes(typeof(DisplayNameAttribute), true)
            .OfType<DisplayNameAttribute>()
            .FirstOrDefault()?.DisplayName
            ?? component.GetType().Name;
    }

    protected static string FormatAssetReference(Guid assetId)
    {
        if (assetId == Guid.Empty)
        {
            return "<none>";
        }

        var assetInfo = AssetCatalog.Get(assetId);
        return assetInfo == null
            ? assetId.ToString()
            : assetInfo.Name;
    }

    protected string BuildComponentCommandDescription(string subject)
        => $"Edit {GetDisplayName(Component)} {subject}";

    protected void ApplyPropertyChange(object target, PropertyDescriptor property, object? newValue, Action? afterApply = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(property);

        var currentValue = property.GetValue(target);
        if (Equals(currentValue, newValue))
        {
            return;
        }

        ExecuteHistoryCommand(
            BuildComponentCommandDescription(property.DisplayName),
            () =>
            {
                property.SetValue(target, newValue);
                afterApply?.Invoke();
            },
            () =>
            {
                property.SetValue(target, currentValue);
                afterApply?.Invoke();
            });
    }

    protected void ApplyValueChange<T>(string description, Func<T> getter, Action<T> setter, T newValue, Action? afterApply = null)
    {
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);

        var currentValue = getter();
        if (EqualityComparer<T>.Default.Equals(currentValue, newValue))
        {
            return;
        }

        ExecuteHistoryCommand(
            description,
            () =>
            {
                setter(newValue);
                afterApply?.Invoke();
            },
            () =>
            {
                setter(currentValue);
                afterApply?.Invoke();
            });
    }

    protected virtual MGElement? CreatePropertyEditor(object target, PropertyDescriptor property)
    {
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var currentValue = property.GetValue(target);

        if (propertyType == typeof(string))
        {
            var textBox = new MGTextBox(Window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            textBox.SetText((string?)currentValue ?? string.Empty);
            textBox.TextChanged += (_, e) => ApplyPropertyChange(target, property, e.NewValue);
            return textBox;
        }

        if (propertyType == typeof(bool))
        {
            var checkBox = new MGCheckBox(Window)
            {
                IsChecked = (bool?)currentValue ?? false,
            };
            checkBox.OnCheckStateChanged += (_, e) => ApplyPropertyChange(target, property, e.NewValue ?? false);
            return checkBox;
        }

        if (propertyType == typeof(int) || propertyType == typeof(float) || propertyType == typeof(double))
        {
            var numericField = new NumericField(Window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            numericField.Value = currentValue switch
            {
                int intValue => intValue,
                float floatValue => floatValue,
                double doubleValue => (float)doubleValue,
                _ => 0f,
            };

            numericField.ValueChanged += (_, value) =>
            {
                object converted = propertyType == typeof(int)
                    ? (int)MathF.Round(value)
                    : propertyType == typeof(double)
                        ? value
                        : value;
                ApplyPropertyChange(target, property, converted);
            };

            return numericField;
        }

        if (propertyType == typeof(Vector3))
        {
            var vectorEditor = new Vector3Editor(Window)
            {
                Value = currentValue is Vector3 vector ? vector : Vector3.Zero,
            };
            vectorEditor.ValueChanged += (_, value) => ApplyPropertyChange(target, property, value);
            return vectorEditor;
        }

        if (propertyType == typeof(Color))
        {
            var colorEditor = new ColorEditor(Window, currentValue is Color color ? color : Color.White);
            colorEditor.ValueChanged += (_, value) => ApplyPropertyChange(target, property, value);
            return colorEditor;
        }

        if (propertyType == typeof(Guid))
        {
            if (property.Name.EndsWith("AssetId", StringComparison.OrdinalIgnoreCase))
            {
                var selector = new AssetSelector(Window)
                {
                    AssetId = currentValue is Guid assetGuid ? assetGuid : Guid.Empty,
                };
                selector.AssetChanged += (_, value) => ApplyPropertyChange(target, property, value);
                return selector;
            }

            var guidBox = new MGTextBox(Window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            guidBox.SetText(currentValue is Guid propertyGuid ? propertyGuid.ToString() : Guid.Empty.ToString());
            guidBox.TextChanged += (_, e) =>
            {
                if (Guid.TryParse(e.NewValue, out var parsedGuid))
                {
                    ApplyPropertyChange(target, property, parsedGuid);
                }
            };
            return guidBox;
        }

        if (propertyType.IsEnum)
        {
            return CreateStringCombo(Enum.GetNames(propertyType), currentValue?.ToString(), value =>
            {
                ApplyPropertyChange(target, property, Enum.Parse(propertyType, value));
            });
        }

        return null;
    }

    private static bool CanEditProperty(
        PropertyDescriptor property,
        ISet<string>? excludedPropertyNames,
        Func<PropertyDescriptor, bool>? includeProperty)
    {
        if (!property.IsBrowsable || property.IsReadOnly)
        {
            return false;
        }

        if (property.Attributes.OfType<ReadOnlyAttribute>().Any(attribute => attribute.IsReadOnly))
        {
            return false;
        }

        if (DefaultUnsupportedPropertyNames.Contains(property.Name))
        {
            return false;
        }

        if (excludedPropertyNames != null && excludedPropertyNames.Contains(property.Name))
        {
            return false;
        }

        return includeProperty?.Invoke(property) ?? true;
    }

    private void ExecuteHistoryCommand(string description, Action execute, Action undo)
    {
        EditorHistoryService.Current.Execute(
            HistoryContext.IsEmpty ? DefaultHistoryContext : HistoryContext,
            new EditorDelegateCommand(description, execute, undo));
    }
}