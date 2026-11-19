using System;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;

namespace CasaEngine.Editor.Runtime;

/// <summary>
/// Creates and configures the editor's own 3D cameras: the edit camera of the world viewport and the cameras of the
/// asset previews. They draw everything in front of them: their far plane is infinite (ADR-0121).
/// <para>
/// <see cref="CameraComponent.InitializeWithWorld"/> resets the depth range to 1..1000, so an editor camera is
/// initialized through <see cref="CreateCameraEntity"/> or <see cref="InitializeWithWorld"/>, which apply the settings
/// afterwards. The play-mode cameras come from the played world and keep their own far plane.
/// </para>
/// </summary>
internal static class EditorCameraFactory
{
    /// <summary>The far plane of every editor camera. With it, the projection is the exact infinite perspective.</summary>
    public const float FarPlane = float.PositiveInfinity;

    /// <summary>Applies the editor settings to <paramref name="camera"/>: the infinite far plane.</summary>
    public static void ApplySettings(Camera3dComponent camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        camera.FarPlane = FarPlane;
    }

    /// <summary>
    /// Creates the hidden entity <paramref name="name"/> that holds <paramref name="camera"/>, initializes it in
    /// <paramref name="world"/> and applies the editor settings.
    /// </summary>
    public static Entity CreateCameraEntity(string name, Camera3dComponent camera, World world)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(world);

        var cameraEntity = new Entity
        {
            Name = name,
            IsVisible = false,
        };

        cameraEntity.AddComponent(camera);
        cameraEntity.Initialize();
        InitializeWithWorld(cameraEntity, camera, world);
        return cameraEntity;
    }

    /// <summary>
    /// Initializes the editor camera entity in <paramref name="world"/>, then applies the editor settings again, since
    /// the initialization resets the far plane.
    /// </summary>
    public static void InitializeWithWorld(Entity cameraEntity, Camera3dComponent camera, World world)
    {
        ArgumentNullException.ThrowIfNull(cameraEntity);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(world);

        cameraEntity.InitializeWithWorld(world);
        ApplySettings(camera);
    }
}
