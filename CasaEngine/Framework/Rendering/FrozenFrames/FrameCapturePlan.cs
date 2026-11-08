namespace CasaEngine.Framework.Rendering.FrozenFrames;

/// <summary>Decision of <see cref="FrameCapturePlanner.Plan"/> (E19.TR-4a, D-TR4A-7).</summary>
/// <param name="UseSceneRt">Draw the view through the scene render target.</param>
/// <param name="CopyRetainedFrameToCapture">Copy the retained frame to the captured image before clearing.</param>
/// <param name="CaptureUnavailable">The waiting request fails (once).</param>
/// <param name="RetainThisFrame">The frame of this view is drawn into the retained target (and kept).</param>
internal readonly record struct FrameCapturePlan(
    bool UseSceneRt,
    bool CopyRetainedFrameToCapture,
    bool CaptureUnavailable,
    bool RetainThisFrame);
