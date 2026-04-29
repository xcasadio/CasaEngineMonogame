using System;
using CasaEngine.Framework.Scene.Entities.Components;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;

namespace CasaEngine.Editor.Controls.ComponentEditors;

public sealed class LightComponentEditor : TransformComponentEditor
{
    private LightComponent LightComponent => (LightComponent)Component;

    public LightComponentEditor(MGWindow window, LightComponent component, Action? refreshRequested = null)
        : base(window, component, refreshRequested)
    {
    }

    protected override void AddAdditionalSections(MGStackPanel root)
    {
        root.TryAddChild(CreateLightSection());
    }

    private MGExpander CreateLightSection()
    {
        var section = CreateSection("Light");
        var grid = CreatePropertyGrid();
        int rowIndex = 0;

        var typeEditor = CreateStringCombo(Enum.GetNames<LightType>(), LightComponent.Type.ToString(), value =>
        {
            ApplyValueChange(
                BuildComponentCommandDescription("Type"),
                () => LightComponent.Type,
                nextValue => LightComponent.Type = nextValue,
                Enum.Parse<LightType>(value),
                RefreshRequested);
        });
        rowIndex = AddPropertyRow(grid, rowIndex, "Type", typeEditor);

        var colorEditor = new ColorEditor(Window, LightComponent.Color);
        colorEditor.ValueChanged += (_, value) => ApplyValueChange(
            BuildComponentCommandDescription("Color"),
            () => LightComponent.Color,
            nextValue => LightComponent.Color = nextValue,
            value);
        rowIndex = AddPropertyRow(grid, rowIndex, "Color", colorEditor);

        var specularColorEditor = new ColorEditor(Window, LightComponent.SpecularColor);
        specularColorEditor.ValueChanged += (_, value) => ApplyValueChange(
            BuildComponentCommandDescription("Specular Color"),
            () => LightComponent.SpecularColor,
            nextValue => LightComponent.SpecularColor = nextValue,
            value);
        rowIndex = AddPropertyRow(grid, rowIndex, "Specular", specularColorEditor);

        var intensityEditor = new NumericField(Window, step: 0.1f, min: 0.0f)
        {
            Value = LightComponent.Intensity,
        };
        intensityEditor.ValueChanged += (_, value) => ApplyValueChange(
            BuildComponentCommandDescription("Intensity"),
            () => LightComponent.Intensity,
            nextValue => LightComponent.Intensity = nextValue,
            value);
        rowIndex = AddPropertyRow(grid, rowIndex, "Intensity", intensityEditor);

        if (LightComponent.Type != LightType.Directional)
        {
            var rangeEditor = new NumericField(Window, step: 0.5f, min: 0.0f)
            {
                Value = LightComponent.Range,
            };
            rangeEditor.ValueChanged += (_, value) => ApplyValueChange(
                BuildComponentCommandDescription("Range"),
                () => LightComponent.Range,
                nextValue => LightComponent.Range = nextValue,
                value);
            rowIndex = AddPropertyRow(grid, rowIndex, "Range", rangeEditor);
        }

        if (LightComponent.Type == LightType.Spot)
        {
            var innerAngleEditor = new NumericField(Window, step: 1.0f, min: 0.0f, max: 89.0f)
            {
                Value = LightComponent.InnerConeAngleDegrees,
            };
            innerAngleEditor.ValueChanged += (_, value) => ApplyValueChange(
                BuildComponentCommandDescription("Inner Cone Angle"),
                () => LightComponent.InnerConeAngleDegrees,
                nextValue => LightComponent.InnerConeAngleDegrees = nextValue,
                value,
                RefreshRequested);
            rowIndex = AddPropertyRow(grid, rowIndex, "Inner Cone", innerAngleEditor);

            var outerAngleEditor = new NumericField(Window, step: 1.0f, min: 0.0f, max: 89.0f)
            {
                Value = LightComponent.OuterConeAngleDegrees,
            };
            outerAngleEditor.ValueChanged += (_, value) => ApplyValueChange(
                BuildComponentCommandDescription("Outer Cone Angle"),
                () => LightComponent.OuterConeAngleDegrees,
                nextValue => LightComponent.OuterConeAngleDegrees = nextValue,
                value,
                RefreshRequested);
            AddPropertyRow(grid, rowIndex, "Outer Cone", outerAngleEditor);
        }

        section.SetContent(grid);
        return section;
    }
}