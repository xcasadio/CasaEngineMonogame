using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// Tracks which render states are currently applied on the <see cref="GraphicsDevice"/>
/// and skips redundant state-change calls.
/// Reset at the start of each frame via <see cref="ResetFrame"/>.
/// </summary>
public sealed class RenderStateCache
{
    private BlendState        _currentBlend;
    private DepthStencilState _currentDepthStencil;
    private RasterizerState   _currentRasterizer;
    private SamplerState      _currentSampler;

    /// <summary>
    /// Applies the material's render states to <paramref name="device"/>,
    /// using sensible defaults when the material leaves a state at <c>null</c>.
    /// Returns <c>true</c> if any state actually changed.
    /// </summary>
    public bool Apply(GraphicsDevice device, in RenderItem item, RenderStats stats = null)
        => Apply(device, item.Material, item.CompiledMaterial, null, null, stats);

    public bool Apply(GraphicsDevice device, MaterialBase material, RenderStats stats = null)
        => Apply(device, material, null, null, null, stats);

    /// <summary>
    /// Applies the material's render states, optionally overriding the rasterizer and/or
    /// sampler state (e.g. per-mesh double-sided / nearest-filtering flags) without mutating
    /// the material itself. Pass <c>null</c> for an override to keep the material's own state.
    /// </summary>
    public bool Apply(
        GraphicsDevice device,
        MaterialBase material,
        RasterizerState rasterizerOverride,
        SamplerState samplerOverride,
        RenderStats stats = null)
        => Apply(device, material, null, rasterizerOverride, samplerOverride, stats);

    private bool Apply(
        GraphicsDevice device,
        MaterialBase material,
        CompiledMaterial compiledMaterial,
        RasterizerState rasterizerOverride,
        SamplerState samplerOverride,
        RenderStats stats)
    {
        bool changed = false;

        var blend     = compiledMaterial?.BlendState        ?? material.BlendState        ?? BlendState.Opaque;
        var depth     = compiledMaterial?.DepthStencilState ?? material.DepthStencilState ?? DepthStencilState.Default;
        var raster    = rasterizerOverride ?? compiledMaterial?.RasterizerState ?? material.RasterizerState ?? RasterizerState.CullCounterClockwise;
        var sampler   = samplerOverride    ?? compiledMaterial?.SamplerState    ?? material.SamplerState    ?? SamplerState.AnisotropicClamp;

        if (!ReferenceEquals(blend, _currentBlend))
        {
            device.BlendState   = blend;
            _currentBlend       = blend;
            changed             = true;
        }

        if (!ReferenceEquals(depth, _currentDepthStencil))
        {
            device.DepthStencilState = depth;
            _currentDepthStencil     = depth;
            changed                  = true;
        }

        if (!ReferenceEquals(raster, _currentRasterizer))
        {
            device.RasterizerState = raster;
            _currentRasterizer     = raster;
            changed                = true;
        }

        if (!ReferenceEquals(sampler, _currentSampler))
        {
            device.SamplerStates[0] = sampler;
            _currentSampler         = sampler;
            changed                 = true;
        }

        if (changed && stats is not null)
        {
            stats.StateChanges++;
        }

        return changed;
    }

    /// <summary>Clears all tracked state references so that the first draw of the next frame always applies the state.</summary>
    public void ResetFrame()
    {
        _currentBlend        = null;
        _currentDepthStencil = null;
        _currentRasterizer   = null;
        _currentSampler      = null;
    }
}
