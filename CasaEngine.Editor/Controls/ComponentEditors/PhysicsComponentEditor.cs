using System;
using System.Collections.Generic;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities.Components;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;

namespace CasaEngine.Editor.Controls.ComponentEditors;

public sealed class PhysicsComponentEditor : TransformComponentEditor
{
    private static readonly HashSet<string> PhysicsExcludedPropertyNames = new(TransformExcludedPropertyNames, StringComparer.OrdinalIgnoreCase)
    {
        nameof(PhysicsBaseComponent.PhysicsDefinition),
        nameof(PhysicsBaseComponent.PhysicsType),
        nameof(PhysicsBaseComponent.Velocity),
        nameof(BoxCollisionComponent.Box),
        nameof(SphereCollisionComponent.Sphere),
        nameof(CapsuleCollisionComponent.Capsule),
        nameof(CylinderCollisionComponent.Cylinder),
    };

    private PhysicsBaseComponent PhysicsComponent => (PhysicsBaseComponent)Component;

    public PhysicsComponentEditor(MGWindow window, PhysicsBaseComponent component)
        : base(window, component)
    {
    }

    protected override void AddAdditionalSections(MGStackPanel root)
    {
        root.TryAddChild(CreatePhysicsSection());

        var shapeSection = CreateShapeSection();
        if (shapeSection != null)
        {
            root.TryAddChild(shapeSection);
        }

        var genericSection = CreateGenericSection(PhysicsComponent, "Properties", PhysicsExcludedPropertyNames);
        if (genericSection != null)
        {
            root.TryAddChild(genericSection);
        }
    }

    private MGExpander CreatePhysicsSection()
    {
        var section = CreateSection("Physics");
        var grid = CreatePropertyGrid();
        int rowIndex = 0;

        var simulatePhysicsCheckBox = new MGCheckBox(Window)
        {
            IsChecked = PhysicsComponent.SimulatePhysics,
        };
        simulatePhysicsCheckBox.OnCheckStateChanged += (_, e) => ApplyValueChange(
            BuildComponentCommandDescription("Simulate Physics"),
            () => PhysicsComponent.SimulatePhysics,
            value => PhysicsComponent.SimulatePhysics = value,
            e.NewValue ?? false);
        rowIndex = AddPropertyRow(grid, rowIndex, "Simulate Physics", simulatePhysicsCheckBox);

        var physicsTypeCombo = CreateStringCombo(Enum.GetNames(typeof(PhysicsType)), PhysicsComponent.PhysicsDefinition.PhysicsType.ToString(), value =>
        {
            ApplyValueChange(
                BuildComponentCommandDescription("Physics Type"),
                () => PhysicsComponent.PhysicsDefinition.PhysicsType,
                nextValue => PhysicsComponent.PhysicsDefinition.PhysicsType = nextValue,
                Enum.Parse<PhysicsType>(value));
        });
        rowIndex = AddPropertyRow(grid, rowIndex, "Physics Type", physicsTypeCombo);

        rowIndex = AddNumericDefinitionRow(grid, rowIndex, "Mass", PhysicsComponent.PhysicsDefinition, () => PhysicsComponent.PhysicsDefinition.Mass, value => PhysicsComponent.PhysicsDefinition.Mass = value, 0.1f, 0f);
        rowIndex = AddNumericDefinitionRow(grid, rowIndex, "Friction", PhysicsComponent.PhysicsDefinition, () => PhysicsComponent.PhysicsDefinition.Friction, value => PhysicsComponent.PhysicsDefinition.Friction = value, 0.05f, 0f);
        rowIndex = AddNumericDefinitionRow(grid, rowIndex, "Restitution", PhysicsComponent.PhysicsDefinition, () => PhysicsComponent.PhysicsDefinition.Restitution, value => PhysicsComponent.PhysicsDefinition.Restitution = value, 0.05f, 0f);
        rowIndex = AddNumericDefinitionRow(grid, rowIndex, "Rolling Friction", PhysicsComponent.PhysicsDefinition, () => PhysicsComponent.PhysicsDefinition.RollingFriction, value => PhysicsComponent.PhysicsDefinition.RollingFriction = value, 0.05f, 0f);

        var applyGravityCheckBox = new MGCheckBox(Window)
        {
            IsChecked = PhysicsComponent.PhysicsDefinition.ApplyGravity,
        };
        applyGravityCheckBox.OnCheckStateChanged += (_, e) => ApplyValueChange(
            BuildComponentCommandDescription("Apply Gravity"),
            () => PhysicsComponent.PhysicsDefinition.ApplyGravity,
            value => PhysicsComponent.PhysicsDefinition.ApplyGravity = value,
            e.NewValue ?? false);
        rowIndex = AddPropertyRow(grid, rowIndex, "Apply Gravity", applyGravityCheckBox);

        var debugColorEditor = new ColorEditor(Window, PhysicsComponent.PhysicsDefinition.DebugColor ?? Microsoft.Xna.Framework.Color.White);
        debugColorEditor.ValueChanged += (_, value) => ApplyValueChange(
            BuildComponentCommandDescription("Debug Color"),
            () => PhysicsComponent.PhysicsDefinition.DebugColor,
            nextValue => PhysicsComponent.PhysicsDefinition.DebugColor = nextValue,
            value);
        AddPropertyRow(grid, rowIndex, "Debug Color", debugColorEditor);

        section.SetContent(grid);
        return section;
    }

    private MGExpander? CreateShapeSection()
    {
        var grid = CreatePropertyGrid();
        int rowIndex = 0;

        switch (PhysicsComponent)
        {
            case BoxCollisionComponent boxCollisionComponent:
            {
                var sizeEditor = new Vector3Editor(Window)
                {
                    Value = boxCollisionComponent.Box.Size,
                };
                sizeEditor.ValueChanged += (_, value) => ApplyValueChange(
                    BuildComponentCommandDescription("Box Size"),
                    () => boxCollisionComponent.Box.Size,
                    nextValue => boxCollisionComponent.Box.Size = nextValue,
                    value);
                rowIndex = AddPropertyRow(grid, rowIndex, "Box Size", sizeEditor);
                break;
            }
            case SphereCollisionComponent sphereCollisionComponent:
            {
                var radiusEditor = new NumericField(Window, step: 0.1f, min: 0f)
                {
                    Value = sphereCollisionComponent.Sphere.Radius,
                };
                radiusEditor.ValueChanged += (_, value) => ApplyValueChange(
                    BuildComponentCommandDescription("Radius"),
                    () => sphereCollisionComponent.Sphere.Radius,
                    nextValue => sphereCollisionComponent.Sphere.Radius = nextValue,
                    value);
                rowIndex = AddPropertyRow(grid, rowIndex, "Radius", radiusEditor);
                break;
            }
            case CapsuleCollisionComponent capsuleCollisionComponent:
            {
                var radiusEditor = new NumericField(Window, step: 0.1f, min: 0f)
                {
                    Value = capsuleCollisionComponent.Capsule.Radius,
                };
                radiusEditor.ValueChanged += (_, value) => ApplyValueChange(
                    BuildComponentCommandDescription("Capsule Radius"),
                    () => capsuleCollisionComponent.Capsule.Radius,
                    nextValue => capsuleCollisionComponent.Capsule.Radius = nextValue,
                    value);
                rowIndex = AddPropertyRow(grid, rowIndex, "Radius", radiusEditor);

                var lengthEditor = new NumericField(Window, step: 0.1f, min: 0f)
                {
                    Value = capsuleCollisionComponent.Capsule.Length,
                };
                lengthEditor.ValueChanged += (_, value) => ApplyValueChange(
                    BuildComponentCommandDescription("Capsule Length"),
                    () => capsuleCollisionComponent.Capsule.Length,
                    nextValue => capsuleCollisionComponent.Capsule.Length = nextValue,
                    value);
                rowIndex = AddPropertyRow(grid, rowIndex, "Length", lengthEditor);
                break;
            }
            case CylinderCollisionComponent cylinderCollisionComponent:
            {
                var radiusEditor = new NumericField(Window, step: 0.1f, min: 0f)
                {
                    Value = cylinderCollisionComponent.Cylinder.Radius,
                };
                radiusEditor.ValueChanged += (_, value) => ApplyValueChange(
                    BuildComponentCommandDescription("Cylinder Radius"),
                    () => cylinderCollisionComponent.Cylinder.Radius,
                    nextValue => cylinderCollisionComponent.Cylinder.Radius = nextValue,
                    value);
                rowIndex = AddPropertyRow(grid, rowIndex, "Radius", radiusEditor);

                var lengthEditor = new NumericField(Window, step: 0.1f, min: 0f)
                {
                    Value = cylinderCollisionComponent.Cylinder.Length,
                };
                lengthEditor.ValueChanged += (_, value) => ApplyValueChange(
                    BuildComponentCommandDescription("Cylinder Length"),
                    () => cylinderCollisionComponent.Cylinder.Length,
                    nextValue => cylinderCollisionComponent.Cylinder.Length = nextValue,
                    value);
                rowIndex = AddPropertyRow(grid, rowIndex, "Length", lengthEditor);
                break;
            }
        }

        if (rowIndex == 0)
        {
            return null;
        }

        var section = CreateSection("Collision Shape");
        section.SetContent(grid);
        return section;
    }

    private int AddNumericDefinitionRow(MGGrid grid, int rowIndex, string label, PhysicsDefinition definition, Func<float> getter, Action<float> setter, float step, float min)
    {
        var editor = new NumericField(Window, step: step, min: min)
        {
            Value = getter(),
        };
        editor.ValueChanged += (_, value) => ApplyValueChange(BuildComponentCommandDescription(label), getter, setter, value);
        return AddPropertyRow(grid, rowIndex, label, editor);
    }
}