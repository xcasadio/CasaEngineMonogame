namespace CasaEngine.Framework.Rendering.FrozenFrames;

/// <summary>Inputs of <see cref="FrameCapturePlanner.Plan"/> for one view (E19.TR-4a, D-TR4A-7).</summary>
/// <param name="ShadowsEnabled">The shadow settings of the view are enabled.</param>
/// <param name="ClearColorBuffer">The view clears its colour buffer.</param>
/// <param name="InitialTargetIsBackBuffer">The render target active on entering the render is the back buffer (null).</param>
/// <param name="SurfaceIsBackBuffer">The surface of the view is the back buffer.</param>
/// <param name="ViewRectangleIsEmpty">The viewport rectangle of the view is empty.</param>
/// <param name="RetentionEnabled">The retention option is on.</param>
/// <param name="IsTargetView">The view is the one the retention targets.</param>
/// <param name="CaptureRequested">A capture request waits.</param>
/// <param name="RetainedFrameValid">A retained frame of the size of the view exists and is not lost.</param>
internal readonly record struct FrameCaptureInputs(
    bool ShadowsEnabled,
    bool ClearColorBuffer,
    bool InitialTargetIsBackBuffer,
    bool SurfaceIsBackBuffer,
    bool ViewRectangleIsEmpty,
    bool RetentionEnabled,
    bool IsTargetView,
    bool CaptureRequested,
    bool RetainedFrameValid);
