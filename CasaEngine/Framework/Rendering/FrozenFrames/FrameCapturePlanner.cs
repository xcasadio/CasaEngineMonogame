namespace CasaEngine.Framework.Rendering.FrozenFrames;

/// <summary>
/// Pure drawing decision of the frozen frame, provable without a GPU (E19.TR-4a, D-TR4A-7).
/// </summary>
internal static class FrameCapturePlanner
{
    internal static FrameCapturePlan Plan(in FrameCaptureInputs inputs)
    {
        // The rule of RenderPipeline.Render before the frozen frame: shadows on a cleared back-buffer view.
        var oldRule = inputs.ShadowsEnabled
            && inputs.ClearColorBuffer
            && inputs.InitialTargetIsBackBuffer
            && inputs.SurfaceIsBackBuffer;

        // The retention: the target view, a cleared back-buffer view of non-empty rectangle, option on.
        var retain = inputs.RetentionEnabled
            && inputs.IsTargetView
            && inputs.ClearColorBuffer
            && inputs.InitialTargetIsBackBuffer
            && inputs.SurfaceIsBackBuffer
            && !inputs.ViewRectangleIsEmpty;

        var copy = false;
        var unavailable = false;
        if (inputs.CaptureRequested && inputs.IsTargetView)
        {
            if (retain && inputs.RetainedFrameValid)
            {
                copy = true;
            }
            else
            {
                unavailable = true;
            }
        }

        return new FrameCapturePlan(oldRule || retain, copy, unavailable, retain);
    }
}
