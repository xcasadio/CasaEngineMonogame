using System;
using System.Collections.Generic;

using CasaEngine.Framework.Scene.Entities.Components;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Controls.ComponentEditors;

public class TransformComponentEditor : ComponentEditorBase
{
    protected static readonly HashSet<string> TransformExcludedPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(SceneComponent.Position),
        nameof(SceneComponent.Scale),
        nameof(SceneComponent.Orientation),
        nameof(SceneComponent.LocalPosition),
        nameof(SceneComponent.LocalScale),
        nameof(SceneComponent.LocalOrientation),
    };

    protected SceneComponent SceneComponent => (SceneComponent)Component;

    public TransformComponentEditor(MGWindow window, SceneComponent component, Action? refreshRequested = null)
        : base(window, component, refreshRequested)
    {
    }

    protected override void BuildEditor(MGStackPanel root)
    {
        root.TryAddChild(CreateTransformSection());
        AddAdditionalSections(root);
    }

    protected virtual void AddAdditionalSections(MGStackPanel root)
    {
        var genericSection = CreateGenericSection(SceneComponent, "Properties", TransformExcludedPropertyNames);
        if (genericSection != null)
        {
            root.TryAddChild(genericSection);
        }
    }

    protected MGExpander CreateTransformSection()
    {
        var section = CreateSection("Transform");
        var grid = CreatePropertyGrid();
        int rowIndex = 0;

        var positionEditor = new Vector3Editor(Window)
        {
            Value = SceneComponent.Coordinates.Position,
        };
        positionEditor.ValueChanged += (_, value) => ApplyValueChange(
            BuildComponentCommandDescription("Position"),
            () => SceneComponent.Coordinates.Position,
            nextValue => SceneComponent.Coordinates.Position = nextValue,
            value);
        rowIndex = AddPropertyRow(grid, rowIndex, "Position", positionEditor);

        var rotationEditor = new Vector3Editor(Window)
        {
            Value = SceneComponent.Coordinates.Orientation.GetYawPitchRoll(),
        };
        rotationEditor.ValueChanged += (_, value) => ApplyValueChange(
            BuildComponentCommandDescription("Rotation"),
            () => SceneComponent.Coordinates.Orientation,
            nextValue => SceneComponent.Coordinates.Orientation = nextValue,
            Quaternion.CreateFromYawPitchRoll(value.X, value.Y, value.Z));
        rowIndex = AddPropertyRow(grid, rowIndex, "Rotation", rotationEditor);

        var scaleEditor = new Vector3Editor(Window)
        {
            Value = SceneComponent.Coordinates.Scale,
        };
        scaleEditor.ValueChanged += (_, value) => ApplyValueChange(
            BuildComponentCommandDescription("Scale"),
            () => SceneComponent.Coordinates.Scale,
            nextValue => SceneComponent.Coordinates.Scale = nextValue,
            value);
        AddPropertyRow(grid, rowIndex, "Scale", scaleEditor);

        section.SetContent(grid);
        return section;
    }
}