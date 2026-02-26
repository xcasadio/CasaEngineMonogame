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

    public void Reset()
    {
        DrawCalls = 0;
        EffectBinds = 0;
        TextureBinds = 0;
        StateChanges = 0;
        OpaqueItems = 0;
        TransparentItems = 0;
    }

    public override string ToString() =>
        $"Draws:{DrawCalls} FX:{EffectBinds} Tex:{TextureBinds} States:{StateChanges} Opaque:{OpaqueItems} Trans:{TransparentItems}";
}
