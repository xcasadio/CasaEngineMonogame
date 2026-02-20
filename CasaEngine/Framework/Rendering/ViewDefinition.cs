using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Immutable description used by <see cref="ViewManager.CreateView(ViewDefinition)"/>
/// to construct and register a new <see cref="RenderView"/>.
/// </summary>
public sealed record ViewDefinition
{
    /// <summary>World to render in this view.</summary>
    public required World.World World { get; init; }

    /// <summary>Camera that provides View/Projection matrices.</summary>
    public required CameraComponent Camera { get; init; }

    /// <summary>Output surface (backbuffer region or RenderTarget).</summary>
    public required IRenderSurface Surface { get; init; }

    /// <summary>Clear color. Default: CornflowerBlue.</summary>
    public Color ClearColor { get; init; } = Color.CornflowerBlue;

    /// <summary>Whether to clear the color buffer before each render. Default: true.</summary>
    public bool ClearColorBuffer { get; init; } = true;

    /// <summary>Whether to clear the depth buffer before each render. Default: true.</summary>
    public bool ClearDepthBuffer { get; init; } = true;

    /// <summary>Optional display name for debugging purposes.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>How often this view is re-rendered. Default: RealTime.</summary>
    public ViewUpdateMode UpdateMode { get; init; } = ViewUpdateMode.RealTime;

    /// <summary>
    /// Target frame-rate for <see cref="ViewUpdateMode.Throttled"/> mode.
    /// Default: 10 fps.
    /// </summary>
    public float TargetFrameRate { get; init; } = 10f;

    /// <summary>
    /// Render resolution multiplier (0.25..2.0, default 1.0).
    /// The surface/RT is created at <c>width * scale</c> and upscaled on presentation.
    /// </summary>
    public float ResolutionScale { get; init; } = 1.0f;

    /// <summary>Optional custom render pipeline. Null = use DefaultViewPipeline.</summary>
    public IViewRenderPipeline? Pipeline { get; init; }

    /// <summary>Optional presenter for post-render display. Null = no extra presentation step.</summary>
    public IViewPresenter? Presenter { get; init; }
}
