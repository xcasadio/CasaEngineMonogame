using System;
using System.Collections.Generic;
using CasaEngine.Framework.Scene.Entities.Components;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;

namespace CasaEngine.Editor.Controls.ComponentEditors;

public sealed class CameraComponentEditor : TransformComponentEditor
{
    private static readonly HashSet<string> CameraExcludedPropertyNames = new(TransformExcludedPropertyNames, StringComparer.OrdinalIgnoreCase)
    {
        nameof(CameraComponent.NearPlane),
        nameof(CameraComponent.FarPlane),
        nameof(Camera3dComponent.FieldOfView),
        nameof(CameraComponent.ViewDistance),
        nameof(CameraComponent.Viewport),
    };

    private CameraComponent CameraComponent => (CameraComponent)Component;

    public CameraComponentEditor(MGWindow window, CameraComponent component)
        : base(window, component)
    {
    }

    protected override void AddAdditionalSections(MGStackPanel root)
    {
        root.TryAddChild(CreateCameraSection());

        var genericSection = CreateGenericSection(CameraComponent, "Properties", CameraExcludedPropertyNames);
        if (genericSection != null)
        {
            root.TryAddChild(genericSection);
        }
    }

    private MGExpander CreateCameraSection()
    {
        var section = CreateSection("Camera");
        var grid = CreatePropertyGrid();
        int rowIndex = 0;

        if (CameraComponent is Camera3dComponent camera3dComponent)
        {
            var fieldOfViewEditor = new NumericField(Window, step: 0.01f)
            {
                Value = camera3dComponent.FieldOfView,
            };
            fieldOfViewEditor.ValueChanged += (_, value) => ApplyValueChange(
                BuildComponentCommandDescription("Field Of View"),
                () => camera3dComponent.FieldOfView,
                nextValue => camera3dComponent.FieldOfView = nextValue,
                value);
            rowIndex = AddPropertyRow(grid, rowIndex, "Field Of View", fieldOfViewEditor);
        }

        var nearPlaneEditor = new NumericField(Window, step: 0.1f, min: 0.001f)
        {
            Value = CameraComponent.NearPlane,
        };
        nearPlaneEditor.ValueChanged += (_, value) => ApplyValueChange(
            BuildComponentCommandDescription("Near Plane"),
            () => CameraComponent.NearPlane,
            nextValue => CameraComponent.NearPlane = nextValue,
            value);
        rowIndex = AddPropertyRow(grid, rowIndex, "Near Plane", nearPlaneEditor);

        var farPlaneEditor = new NumericField(Window, step: 1f, min: 0.01f)
        {
            Value = CameraComponent.FarPlane,
        };
        farPlaneEditor.ValueChanged += (_, value) => ApplyValueChange(
            BuildComponentCommandDescription("Far Plane"),
            () => CameraComponent.FarPlane,
            nextValue => CameraComponent.FarPlane = nextValue,
            value);
        AddPropertyRow(grid, rowIndex, "Far Plane", farPlaneEditor);

        section.SetContent(grid);
        return section;
    }
}