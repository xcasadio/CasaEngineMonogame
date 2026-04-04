using System;
using CasaEngine.Framework.Entities.Components;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls.ComponentEditors;

public static class ComponentEditorRegistry
{
    private static readonly (Type ComponentType, Func<MGWindow, EntityComponent, Action?, ComponentEditorBase> Factory)[] Registrations =
    [
        (typeof(StaticModelComponent), (window, component, refreshRequested) => new StaticModelComponentEditor(window, (StaticModelComponent)component, refreshRequested)),
        (typeof(StaticModelSubMeshComponent), (window, component, _) => new StaticModelSubMeshComponentEditor(window, (StaticModelSubMeshComponent)component)),
        (typeof(CameraComponent), (window, component, _) => new CameraComponentEditor(window, (CameraComponent)component)),
        (typeof(PhysicsBaseComponent), (window, component, _) => new PhysicsComponentEditor(window, (PhysicsBaseComponent)component)),
        (typeof(SceneComponent), (window, component, _) => new TransformComponentEditor(window, (SceneComponent)component)),
        (typeof(EntityComponent), (window, component, _) => new GenericComponentEditor(window, component)),
    ];

    public static ComponentEditorBase Create(MGWindow window, EntityComponent component, Action? refreshRequested = null)
    {
        foreach (var registration in Registrations)
        {
            if (registration.ComponentType.IsInstanceOfType(component))
            {
                return registration.Factory(window, component, refreshRequested);
            }
        }

        return new GenericComponentEditor(window, component);
    }
}