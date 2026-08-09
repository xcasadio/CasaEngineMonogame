using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Runtime;

/// <summary>
/// View state of the world viewport persisted next to the editor dock layout:
/// which projection mode the viewport is in, and the framing of its 2d camera.
/// </summary>
internal readonly struct EditorViewportViewState
{
    public EditorViewportViewState(bool is2dViewMode, in EditorViewport2dCameraState camera2dState)
    {
        Is2dViewMode = is2dViewMode;
        Camera2dState = camera2dState;
    }

    public bool Is2dViewMode { get; }

    public EditorViewport2dCameraState Camera2dState { get; }
}

/// <summary>
/// Json serialization of <see cref="EditorViewportViewState"/>. Kept free of any file or
/// graphics dependency so that it can be unit tested and reused by other viewport hosts.
/// </summary>
internal static class EditorViewportViewStateSerializer
{
    private const string WorldViewportKey = "world_viewport";

    public static JObject Save(in EditorViewportViewState state)
    {
        var camera = state.Camera2dState;

        return new JObject
        {
            [WorldViewportKey] = new JObject
            {
                ["is_2d_view_mode"] = state.Is2dViewMode,
                ["target_x"] = camera.Target.X,
                ["target_y"] = camera.Target.Y,
                ["target_z"] = camera.Target.Z,
                ["zoom_step"] = camera.ZoomStep,
                ["pixel_snap"] = camera.PixelSnap,
            },
        };
    }

    public static bool TryLoad(JObject json, out EditorViewportViewState state)
    {
        state = default;

        if (json?[WorldViewportKey] is not JObject viewport)
        {
            return false;
        }

        var target = new Vector3(
            viewport["target_x"]?.Value<float>() ?? 0.0f,
            viewport["target_y"]?.Value<float>() ?? 0.0f,
            viewport["target_z"]?.Value<float>() ?? 0.0f);

        var cameraState = new EditorViewport2dCameraState(
            target,
            viewport["zoom_step"]?.Value<int>() ?? 0,
            viewport["pixel_snap"]?.Value<bool>() ?? true);

        state = new EditorViewportViewState(
            viewport["is_2d_view_mode"]?.Value<bool>() ?? false,
            cameraState);
        return true;
    }
}
