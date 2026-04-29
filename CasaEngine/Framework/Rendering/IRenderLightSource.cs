namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Runtime light source contract consumed by render-light collectors.
/// Authoring components may implement this without the render pipeline depending on their concrete type.
/// </summary>
public interface IRenderLightSource
{
    void AppendLights(LightingContext lightingContext);
}