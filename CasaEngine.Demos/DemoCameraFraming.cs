using System;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos;

/// <summary>
/// Frames a demo camera: places it at a position, looking at a target.
/// <para/>
/// A camera posed by an <see cref="OrbitCameraComponent"/> (the default demo camera, ADR-0145) is framed through the
/// orbit, which owns the pose: a pose written directly on the camera would be overwritten at the first mouse gesture.
/// Any other camera is framed directly.
/// </summary>
public static class DemoCameraFraming
{
    public static void Frame(CameraComponent camera, Vector3 position, Vector3 target)
    {
        ArgumentNullException.ThrowIfNull(camera);

        OrbitCameraComponent orbit = camera.Owner?.GetComponent<OrbitCameraComponent>();
        if (orbit != null)
        {
            orbit.SetCamera(position, target);
            return;
        }

        camera.SetPositionAndTarget(position, target);
    }
}
