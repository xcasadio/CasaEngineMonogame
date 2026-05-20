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
    public double TotalCpuMilliseconds { get; set; }
    public double BeforeViewCpuMilliseconds { get; set; }
    public double ClearCpuMilliseconds { get; set; }
    public double WorldDrawCpuMilliseconds { get; set; }
    public double RendererFlushCpuMilliseconds { get; set; }
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
        TotalCpuMilliseconds = 0.0;
        BeforeViewCpuMilliseconds = 0.0;
        ClearCpuMilliseconds = 0.0;
        WorldDrawCpuMilliseconds = 0.0;
        RendererFlushCpuMilliseconds = 0.0;
        UiComposeCpuMilliseconds = 0.0;
        PresenterCpuMilliseconds = 0.0;
        OverlayCpuMilliseconds = 0.0;
    }

    public override string ToString() =>
        $"Draws:{DrawCalls} FX:{EffectBinds} Tex:{TextureBinds} States:{StateChanges} Opaque:{OpaqueItems} Trans:{TransparentItems} Particles:{ParticleCount}";
}
