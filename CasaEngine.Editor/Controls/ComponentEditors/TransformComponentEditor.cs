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

    private Vector3Editor? _positionEditor;
    private Vector3Editor? _rotationEditor;
    private Vector3Editor? _scaleEditor;

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

    public override bool TryRefreshFromComponent()
    {
        if (_positionEditor == null || _rotationEditor == null || _scaleEditor == null)
        {
            return false;
        }

        _positionEditor.Value = SceneComponent.Coordinates.Position;
        _rotationEditor.Value = SceneComponent.Coordinates.Orientation.GetYawPitchRoll();
        _scaleEditor.Value = SceneComponent.Coordinates.Scale;
        return true;
    }

    protected MGExpander CreateTransformSection()
    {
        var section = CreateSection("Transform");
        var grid = CreatePropertyGrid();
        int rowIndex = 0;

        _positionEditor = new Vector3Editor(Window)
        {
            Value = SceneComponent.Coordinates.Position,
        };
        _positionEditor.ValueChanged += (_, value) => ApplyValueChange(
            BuildComponentCommandDescription("Position"),
            () => SceneComponent.Coordinates.Position,
            nextValue => SceneComponent.Coordinates.Position = nextValue,
            value);
        rowIndex = AddPropertyRow(grid, rowIndex, "Position", _positionEditor);

        _rotationEditor = new Vector3Editor(Window)
        {
            Value = SceneComponent.Coordinates.Orientation.GetYawPitchRoll(),
        };
        _rotationEditor.ValueChanged += (_, value) => ApplyValueChange(
            BuildComponentCommandDescription("Rotation"),
            () => SceneComponent.Coordinates.Orientation,
            nextValue => SceneComponent.Coordinates.Orientation = nextValue,
            Quaternion.CreateFromYawPitchRoll(value.X, value.Y, value.Z));
        rowIndex = AddPropertyRow(grid, rowIndex, "Rotation", _rotationEditor);

        _scaleEditor = new Vector3Editor(Window)
        {
            Value = SceneComponent.Coordinates.Scale,
        };
        _scaleEditor.ValueChanged += (_, value) => ApplyValueChange(
            BuildComponentCommandDescription("Scale"),
            () => SceneComponent.Coordinates.Scale,
            nextValue => SceneComponent.Coordinates.Scale = nextValue,
            value);
        AddPropertyRow(grid, rowIndex, "Scale", _scaleEditor);

        section.SetContent(grid);
        return section;
    }
}