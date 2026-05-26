using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Shadows;

/// <summary>
/// Reusable per-view shadow resources for the forward renderer.
/// Owns the atlas target reference and the visible shadow-casting lights selected for the view.
/// </summary>
public sealed class ForwardShadowResources
{
    private readonly List<ShadowLight> _visibleLights = new(
        LightingContext.MaxDirectionalLights + LightingContext.MaxSpotLights + LightingContext.MaxPointLights);

    public ShadowSettings Settings { get; } = new();

    public RenderTarget2D ShadowMapAtlas { get; set; }

    public IReadOnlyList<ShadowLight> VisibleLights => _visibleLights;

    public int VisibleLightCount => _visibleLights.Count;

    public void AddVisibleLight(in ShadowLight shadowLight)
    {
        _visibleLights.Add(shadowLight);
    }

    public void Clear()
    {
        _visibleLights.Clear();
        ShadowMapAtlas = null;
    }
}