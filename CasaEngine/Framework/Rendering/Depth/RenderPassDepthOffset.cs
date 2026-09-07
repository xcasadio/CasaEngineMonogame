namespace CasaEngine.Framework.Rendering.Depth;

/// <summary>
/// Folds a <see cref="RenderPass2D"/> into a scalar Z offset for tile map layers that stay in the
/// static chunked draw path (see D1 of <c>ai-agent/tasks/tilemap-depth-settings-tasks.md</c>).
///
/// <see cref="DeriveDepthOffset"/> is pure and monotonic in <see cref="RenderPass2D"/>'s declared
/// order: exactly 0 for <see cref="RenderPass2D.YSortedWorld"/>, negative before it, positive after,
/// with a step of 1 between neighbouring passes - well above any per-layer <c>zOffset</c> in existing
/// content, which stays below 1. That step is what lets the pass dominate <c>zOffset</c> without
/// disturbing the fine ordering <c>zOffset</c> already provides between layers of the same pass.
/// </summary>
public static class RenderPassDepthOffset
{
    private const float Step = 1f;

    public static float DeriveDepthOffset(RenderPass2D renderPass) => renderPass switch
    {
        RenderPass2D.Background => -3f * Step,
        RenderPass2D.Ground => -2f * Step,
        RenderPass2D.GroundDetails => -1f * Step,
        RenderPass2D.YSortedWorld => 0f,
        RenderPass2D.Foreground => 1f * Step,
        RenderPass2D.Effects => 2f * Step,
        RenderPass2D.ScreenEffects => 3f * Step,
        RenderPass2D.UI => 4f * Step,
        _ => 0f
    };
}
