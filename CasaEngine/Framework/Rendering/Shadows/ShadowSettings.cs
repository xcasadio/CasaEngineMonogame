namespace CasaEngine.Framework.Rendering.Shadows;

/// <summary>
/// Authoring/runtime settings that control forward shadow-map generation.
/// V1 keeps the feature disabled by default so existing scenes remain unchanged.
/// </summary>
public sealed class ShadowSettings
{
    public bool Enabled { get; set; } = false;

    public int Resolution { get; set; } = 1024;

    public float DepthBias { get; set; } = 0.001f;

    public float NormalBias { get; set; } = 0.0f;

    public float MaxDistance { get; set; } = 100.0f;
}