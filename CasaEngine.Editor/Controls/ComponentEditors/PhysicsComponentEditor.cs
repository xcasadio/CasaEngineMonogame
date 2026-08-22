using System;
using System.Collections.Generic;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
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
        nameof(PhysicsBaseComponent.Bodies),
        nameof(CollisionComponent.Fixtures),
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
        rowIndex = AddNumericDefinitionRow(grid, rowIndex, "Sleep Threshold", PhysicsComponent.PhysicsDefinition, () => PhysicsComponent.PhysicsDefinition.SleepThreshold, value => PhysicsComponent.PhysicsDefinition.SleepThreshold = value, 0.01f, -1f);

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

    private MGExpander CreateShapeSection()
    {
        if (PhysicsComponent is not CollisionComponent collisionComponent)
        {
            return null;
        }

        var grid = CreatePropertyGrid();
        int rowIndex = 0;

        for (int fixtureIndex = 0; fixtureIndex < collisionComponent.Fixtures.Count; fixtureIndex++)
        {
            var fixture = collisionComponent.Fixtures[fixtureIndex];
            string prefix = $"Fixture {fixtureIndex}";

            rowIndex = AddPropertyRow(grid, rowIndex, prefix, new MGTextBlock(Window, GetShapeTypeLabel(fixture)));
            rowIndex = AddShapeRows(grid, rowIndex, fixture, prefix);

            var localPositionEditor = new Vector3Editor(Window)
            {
                Value = fixture.LocalPosition,
            };
            localPositionEditor.ValueChanged += (_, value) => ApplyValueChange(
                BuildComponentCommandDescription($"{prefix} Local Position"),
                () => fixture.LocalPosition,
                nextValue => fixture.LocalPosition = nextValue,
                value);
            rowIndex = AddPropertyRow(grid, rowIndex, "Local Position", localPositionEditor);

            var localRotationEditor = new Vector3Editor(Window)
            {
                Value = ToEulerDegrees(fixture.LocalRotation),
            };
            localRotationEditor.ValueChanged += (_, value) => ApplyValueChange(
                BuildComponentCommandDescription($"{prefix} Local Rotation"),
                () => fixture.LocalRotation,
                nextValue => fixture.LocalRotation = nextValue,
                FromEulerDegrees(value));
            rowIndex = AddPropertyRow(grid, rowIndex, "Local Rotation", localRotationEditor);

            rowIndex = AddStringRow(grid, rowIndex, "Collision Profile", $"{prefix} Collision Profile",
                () => fixture.ProfileName, value => fixture.ProfileName = value);

            rowIndex = AddStringRow(grid, rowIndex, "Tag", $"{prefix} Tag",
                () => fixture.Tag, value => fixture.Tag = value);
        }

        if (rowIndex == 0)
        {
            return null;
        }

        var section = CreateSection("Collision Fixtures");
        section.SetContent(grid);
        return section;
    }

    private static string GetShapeTypeLabel(ColliderFixture fixture)
    {
        return fixture.Shape == null ? "<none>" : Enum.GetName(fixture.Shape.Type);
    }

    private int AddShapeRows(MGGrid grid, int rowIndex, ColliderFixture fixture, string prefix)
    {
        switch (fixture.Shape)
        {
            case Box box:
            {
                var sizeEditor = new Vector3Editor(Window)
                {
                    Value = box.Size,
                };
                sizeEditor.ValueChanged += (_, value) => ApplyValueChange(
                    BuildComponentCommandDescription($"{prefix} Box Size"),
                    () => box.Size,
                    nextValue => box.Size = nextValue,
                    value);
                return AddPropertyRow(grid, rowIndex, "Box Size", sizeEditor);
            }

            case Sphere sphere:
                return AddNumericShapeRow(grid, rowIndex, "Radius", $"{prefix} Radius", () => sphere.Radius, value => sphere.Radius = value);

            case Capsule capsule:
                rowIndex = AddNumericShapeRow(grid, rowIndex, "Radius", $"{prefix} Capsule Radius", () => capsule.Radius, value => capsule.Radius = value);
                return AddNumericShapeRow(grid, rowIndex, "Length", $"{prefix} Capsule Length", () => capsule.Length, value => capsule.Length = value);

            case Cylinder cylinder:
                rowIndex = AddNumericShapeRow(grid, rowIndex, "Radius", $"{prefix} Cylinder Radius", () => cylinder.Radius, value => cylinder.Radius = value);
                return AddNumericShapeRow(grid, rowIndex, "Length", $"{prefix} Cylinder Length", () => cylinder.Length, value => cylinder.Length = value);

            default:
                return rowIndex;
        }
    }

    private int AddNumericShapeRow(MGGrid grid, int rowIndex, string label, string commandSubject, Func<float> getter, Action<float> setter)
    {
        var editor = new NumericField(Window, step: 0.1f, min: 0f)
        {
            Value = getter(),
        };
        editor.ValueChanged += (_, value) => ApplyValueChange(BuildComponentCommandDescription(commandSubject), getter, setter, value);
        return AddPropertyRow(grid, rowIndex, label, editor);
    }

    private int AddStringRow(MGGrid grid, int rowIndex, string label, string commandSubject, Func<string> getter, Action<string> setter)
    {
        var textBox = new MGTextBox(Window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        textBox.SetText(getter() ?? string.Empty);
        textBox.TextChanged += (_, e) => ApplyValueChange(BuildComponentCommandDescription(commandSubject), getter, setter, e.NewValue);
        return AddPropertyRow(grid, rowIndex, label, textBox);
    }

    private static Vector3 ToEulerDegrees(Quaternion rotation)
    {
        var matrix = Matrix.CreateFromQuaternion(rotation);
        float pitch = (float)Math.Asin(-Math.Clamp(matrix.M32, -1.0f, 1.0f));
        float yaw = (float)Math.Atan2(matrix.M31, matrix.M33);
        float roll = (float)Math.Atan2(matrix.M12, matrix.M22);
        return new Vector3(MathHelper.ToDegrees(pitch), MathHelper.ToDegrees(yaw), MathHelper.ToDegrees(roll));
    }

    private static Quaternion FromEulerDegrees(Vector3 eulerDegrees)
    {
        return Quaternion.CreateFromYawPitchRoll(
            MathHelper.ToRadians(eulerDegrees.Y),
            MathHelper.ToRadians(eulerDegrees.X),
            MathHelper.ToRadians(eulerDegrees.Z));
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