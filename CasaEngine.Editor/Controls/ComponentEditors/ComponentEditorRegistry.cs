using System;
using CasaEngine.Framework.Entities.Components;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls.ComponentEditors;

public static class ComponentEditorRegistry
{
    private static readonly (Type ComponentType, Func<MGWindow, EntityComponent, ComponentEditorBase> Factory)[] Registrations =
    [
        (typeof(StaticModelComponent), (window, component) => new StaticModelComponentEditor(window, (StaticModelComponent)component)),
        (typeof(CameraComponent), (window, component) => new CameraComponentEditor(window, (CameraComponent)component)),
        (typeof(PhysicsBaseComponent), (window, component) => new PhysicsComponentEditor(window, (PhysicsBaseComponent)component)),
        (typeof(SceneComponent), (window, component) => new TransformComponentEditor(window, (SceneComponent)component)),
        (typeof(EntityComponent), (window, component) => new GenericComponentEditor(window, component)),
    ];

    public static ComponentEditorBase Create(MGWindow window, EntityComponent component)
    {
        foreach (var registration in Registrations)
        {
            if (registration.ComponentType.IsInstanceOfType(component))
            {
                return registration.Factory(window, component);
            }
        }

        return new GenericComponentEditor(window, component);
    }
}