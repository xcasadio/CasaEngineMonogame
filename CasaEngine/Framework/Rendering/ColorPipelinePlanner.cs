using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.FrozenFrames;

namespace CasaEngine.Framework.Rendering;

/// <summary>Where the tonemapped image of a view is written (ADR-0096): the destination the legacy path draws into.</summary>
internal enum ColorPipelineDestination
{
    /// <summary>No HDR scene target: the view is drawn by the legacy path.</summary>
    None,

    /// <summary>The render target active when the render began (the back buffer, or the editor's host texture), in the view rectangle.</summary>
    InitialTarget,

    /// <summary>The shared scene target, copied to the initial target afterwards (shadows on a cleared back-buffer view).</summary>
    SceneRt,

    /// <summary>The frozen frame's retained target, copied to the initial target afterwards.</summary>
    RetainedTarget,

    /// <summary>The render target of the view's own surface (render-to-texture, buffered virtual resolution).</summary>
    ViewSurface,
}

/// <summary>Inputs of <see cref="ColorPipelinePlanner.Plan"/> for one view.</summary>
/// <param name="Mode">The colour pipeline of the view's environment source.</param>
/// <param name="ClearColorBuffer">The view clears its colour buffer.</param>
/// <param name="ViewRectangleIsEmpty">The viewport rectangle of the view is empty.</param>
/// <param name="SurfaceIsBackBuffer">The surface of the view is the back buffer.</param>
/// <param name="CapturePlan">The drawing decision of the frozen frame planner for the same view.</param>
internal readonly record struct ColorPipelineInputs(
    ColorPipelineMode Mode,
    bool ClearColorBuffer,
    bool ViewRectangleIsEmpty,
    bool SurfaceIsBackBuffer,
    FrameCapturePlan CapturePlan);

/// <summary>Decision of <see cref="ColorPipelinePlanner.Plan"/>.</summary>
/// <param name="UseHdrSceneTarget">The scene of the view is rendered into a pooled HDR target, then tonemapped.</param>
/// <param name="Destination">Where the tonemapping writes; <see cref="ColorPipelineDestination.None"/> without an HDR target.</param>
internal readonly record struct ColorPipelinePlan(bool UseHdrSceneTarget, ColorPipelineDestination Destination);

/// <summary>
/// Pure decision of the colour pipeline, provable without a GPU (ADR-0096). In <see cref="ColorPipelineMode.Legacy"/> the
/// plan is always "no HDR target", so the legacy path is the one that ran before the pipeline existed.
/// </summary>
internal static class ColorPipelinePlanner
{
    internal static ColorPipelinePlan Plan(in ColorPipelineInputs inputs)
    {
        if (inputs.Mode != ColorPipelineMode.LinearHdr)
        {
            return default;
        }

        // An empty rectangle has nothing to draw. A view that keeps the previous content of its colour buffer cannot be
        // tonemapped without erasing it: the tonemapping overwrites the whole rectangle.
        if (inputs.ViewRectangleIsEmpty || !inputs.ClearColorBuffer)
        {
            return default;
        }

        if (inputs.CapturePlan.UseSceneRt)
        {
            return new ColorPipelinePlan(
                true,
                inputs.CapturePlan.RetainThisFrame ? ColorPipelineDestination.RetainedTarget : ColorPipelineDestination.SceneRt);
        }

        return new ColorPipelinePlan(
            true,
            inputs.SurfaceIsBackBuffer ? ColorPipelineDestination.InitialTarget : ColorPipelineDestination.ViewSurface);
    }
}
