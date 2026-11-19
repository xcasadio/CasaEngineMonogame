using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.PostProcess;

/// <summary>
/// Host of the scene depth pre-pass (ADR-0116). One instance is owned by <see cref="RenderPipeline"/> and handed to the renderers
/// through <see cref="RenderFrame.SceneDepth"/> and <see cref="RenderContext.SceneDepth"/>, as the IBL cache is.
/// <para>
/// For a view that asks for it, <see cref="RenderPipeline"/> acquires a floating-point target (<c>Single</c>, <c>HalfSingle</c> or
/// <c>HalfVector4</c> with a 24-bit depth buffer), <see cref="Activate"/>s it (cleared to <see cref="BackgroundDepth"/>) before the
/// scene is rendered and <see cref="Deactivate"/>s it, then releases it, when the view is done. While it is active, the static and
/// the skinned mesh renderers each write the view depth of their opaque and alpha-tested geometry into it during their own
/// <c>Flush</c>, between <see cref="BeginDraw"/> and <see cref="EndDraw"/>, which bind the target and give the previous binding and
/// states back. Because the pooled targets are <see cref="RenderTargetUsage.PreserveContents"/>, the colour and the depth buffer
/// of the target survive the round trips, so both renderers share one depth buffer and the depth is correct across them.
/// </para>
/// <para>
/// The value written is the clip-space w of a perspective projection: the distance along the camera axis, in world units, not
/// normalised. A pixel no geometry covers keeps <see cref="BackgroundDepth"/>; a reader treats anything beyond
/// <see cref="SkyThreshold"/> as sky. The limit: the far plane of a camera may be set without bound, and geometry farther than
/// <see cref="SkyThreshold"/> units from the camera plane reads as sky.
/// </para>
/// </summary>
public sealed class SceneDepthPrepass
{
    /// <summary>
    /// Value of a pixel no geometry covers (<c>SceneDepthBackground</c>). Exactly representable in every format the pre-pass can
    /// use (the largest <c>Half</c> is 65 504), and above <see cref="SkyThreshold"/>.
    /// </summary>
    public const float BackgroundDepth = 60000.0f;

    /// <summary>A depth at or beyond this value is sky for a reader of the target.</summary>
    public const float SkyThreshold = 30000.0f;

    /// <summary>Static meshes, no texture.</summary>
    internal const string SolidTechniqueName = "SceneDepth_Solid";

    /// <summary>Static meshes with an alpha cut-out.</summary>
    internal const string TexturedTechniqueName = "SceneDepth_Textured";

    /// <summary>Skinned meshes, linear blend skinning.</summary>
    internal const string SkinnedTechniqueName = "RiggedModelSceneDepth";

    /// <summary>Skinned meshes, dual quaternion skinning.</summary>
    internal const string SkinnedDualQuaternionTechniqueName = "RiggedModelSceneDepthDualQuaternion";

    // The binding of the target that was bound when BeginDraw was called, kept in an array that is reused, so the round trip
    // allocates nothing (GraphicsStateGuard allocates one array per capture).
    private readonly RenderTargetBinding[] _savedBinding = new RenderTargetBinding[1];

    private RenderTarget2D _target;
    private bool _drawing;
    private Viewport _savedViewport;
    private Rectangle _savedScissor;
    private BlendState _savedBlend;
    private DepthStencilState _savedDepthStencil;
    private RasterizerState _savedRasterizer;
    private SamplerState _savedSampler;

    internal SceneDepthPrepass()
    {
    }

    /// <summary>True while a view is being rendered with its pre-pass: the renderers draw their depth.</summary>
    public bool IsActive => _target != null;

    /// <summary>The pre-pass target of the active view, or null when none is active.</summary>
    internal RenderTarget2D Target => _target;

    /// <summary>
    /// Makes <paramref name="target"/> the pre-pass target of the view being rendered and clears it to
    /// <see cref="BackgroundDepth"/> (colour) and 1 (depth buffer), then binds the previous target again. Returns false, leaving the
    /// pre-pass inactive, when the device does not have exactly one target bound.
    /// </summary>
    internal bool Activate(GraphicsDevice device, RenderTarget2D target)
    {
        _target = target;
        if (!BeginDraw(device))
        {
            _target = null;
            return false;
        }

        try
        {
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Vector4(BackgroundDepth, 0.0f, 0.0f, 0.0f), 1.0f, 0);
        }
        finally
        {
            EndDraw(device);
        }

        return true;
    }

    /// <summary>Ends the pre-pass of the view and returns its target, which the caller releases; null when none was active.</summary>
    internal RenderTarget2D Deactivate()
    {
        var target = _target;
        _target = null;
        return target;
    }

    /// <summary>
    /// Binds the pre-pass target with a full-target viewport and the states of an opaque depth write (no blending, depth test and
    /// write), after saving the target binding, the viewport, the scissor rectangle, the blend, depth, rasterizer and first sampler
    /// states. Returns false (nothing changed) when the pre-pass is not active, a draw is already open, or the device does not
    /// have exactly one target bound (the scene target of an HDR view).
    /// </summary>
    internal bool BeginDraw(GraphicsDevice device)
    {
        if (_target == null || _drawing || device.RenderTargetCount != 1)
        {
            return false;
        }

        device.GetRenderTargets(_savedBinding);
        _savedViewport = device.Viewport;
        _savedScissor = device.ScissorRectangle;
        _savedBlend = device.BlendState;
        _savedDepthStencil = device.DepthStencilState;
        _savedRasterizer = device.RasterizerState;
        _savedSampler = device.SamplerStates[0];

        device.SetRenderTarget(_target);
        device.Viewport = new Viewport(0, 0, _target.Width, _target.Height);
        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
        _drawing = true;
        return true;
    }

    /// <summary>Gives back what <see cref="BeginDraw"/> saved. Does nothing when no draw is open.</summary>
    internal void EndDraw(GraphicsDevice device)
    {
        if (!_drawing)
        {
            return;
        }

        _drawing = false;
        device.SetRenderTargets(_savedBinding);
        _savedBinding[0] = default;
        device.Viewport = _savedViewport;
        device.ScissorRectangle = _savedScissor;
        device.BlendState = _savedBlend;
        device.DepthStencilState = _savedDepthStencil;
        device.RasterizerState = _savedRasterizer;
        device.SamplerStates[0] = _savedSampler;
        _savedBlend = null;
        _savedDepthStencil = null;
        _savedRasterizer = null;
        _savedSampler = null;
    }
}
