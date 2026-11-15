namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Tracks per-frame rendering statistics: draw calls, shader binds, texture binds, state changes.
/// Reset at the beginning of every frame.
/// </summary>
public class RenderStats
{
    public int DrawCalls { get; set; }
    public int EffectBinds { get; set; }
    public int TextureBinds { get; set; }
    public int StateChanges { get; set; }
    public int OpaqueItems { get; set; }
    public int TransparentItems { get; set; }
    public int ParticleCount { get; set; }
    public int LineCount { get; set; }
    public bool RenderedThisFrame { get; set; }

    /// <summary>Number of FXAA passes drawn for the view in the frame (ADR-0113): 1 when the view was anti-aliased, 0 otherwise.</summary>
    public int FxaaPasses { get; set; }

    /// <summary>Number of SSAO compositions drawn for the view in the frame (ADR-0115): 1 when the occlusion was multiplied into the scene, 0 otherwise.</summary>
    public int SsaoPasses { get; set; }

    /// <summary>
    /// Static mesh items the scene depth pre-pass drew for the view in the frame (ADR-0115): equals <see cref="OpaqueItems"/> of the same
    /// flush when the pre-pass was active (the filter is the one of the opaque pass), 0 otherwise.
    /// </summary>
    public int SceneDepthStaticDraws { get; set; }

    /// <summary>Skinned meshes the scene depth pre-pass drew for the view in the frame (ADR-0115); 0 when the pre-pass was not active.</summary>
    public int SceneDepthSkinnedDraws { get; set; }
    public double TotalCpuMilliseconds { get; set; }
    public double BeforeViewCpuMilliseconds { get; set; }
    public double ClearCpuMilliseconds { get; set; }
    public double WorldDrawCpuMilliseconds { get; set; }
    public double RendererFlushCpuMilliseconds { get; set; }
    public double ParticleRenderCpuMilliseconds { get; set; }
    public double UiComposeCpuMilliseconds { get; set; }
    public double PresenterCpuMilliseconds { get; set; }
    public double OverlayCpuMilliseconds { get; set; }

    public void Reset()
    {
        DrawCalls = 0;
        EffectBinds = 0;
        TextureBinds = 0;
        StateChanges = 0;
        OpaqueItems = 0;
        TransparentItems = 0;
        ParticleCount = 0;
        LineCount = 0;
        RenderedThisFrame = false;
        FxaaPasses = 0;
        SsaoPasses = 0;
        SceneDepthStaticDraws = 0;
        SceneDepthSkinnedDraws = 0;
        TotalCpuMilliseconds = 0.0;
        BeforeViewCpuMilliseconds = 0.0;
        ClearCpuMilliseconds = 0.0;
        WorldDrawCpuMilliseconds = 0.0;
        RendererFlushCpuMilliseconds = 0.0;
        ParticleRenderCpuMilliseconds = 0.0;
        UiComposeCpuMilliseconds = 0.0;
        PresenterCpuMilliseconds = 0.0;
        OverlayCpuMilliseconds = 0.0;
    }

    public override string ToString() =>
        $"Draws:{DrawCalls} FX:{EffectBinds} Tex:{TextureBinds} States:{StateChanges} Opaque:{OpaqueItems} Trans:{TransparentItems} Particles:{ParticleCount} ParticleRender:{ParticleRenderCpuMilliseconds:F2}ms";
}
