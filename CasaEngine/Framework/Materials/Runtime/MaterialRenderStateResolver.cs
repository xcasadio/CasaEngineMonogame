namespace CasaEngine.Framework.Materials.Runtime;

public readonly struct ResolvedMaterialRenderState
{
    public ResolvedMaterialRenderState(
        bool isTransparent,
        RenderQueue queue,
        string blendStateName,
        string depthStencilStateName)
    {
        IsTransparent = isTransparent;
        Queue = queue;
        BlendStateName = blendStateName;
        DepthStencilStateName = depthStencilStateName;
    }

    public bool IsTransparent { get; }

    public RenderQueue Queue { get; }

    public string BlendStateName { get; }

    public string DepthStencilStateName { get; }
}

/// <summary>
/// Centralizes how authoring-time material values translate into runtime pipeline state.
/// Queue/blend/depth defaults should be resolved here instead of being inferred ad hoc by
/// shader selection or individual demos.
/// </summary>
public static class MaterialRenderStateResolver
{
    public static ResolvedMaterialRenderState Resolve(
        MaterialAsset materialAsset,
        MaterialDefinition definition,
        IReadOnlyDictionary<string, MaterialValue> effectiveValues)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(effectiveValues);

        if (materialAsset.Queue == RenderQueue.AlphaTest)
        {
            return new ResolvedMaterialRenderState(
                isTransparent: false,
                queue: RenderQueue.AlphaTest,
                blendStateName: materialAsset.BlendStateName,
                depthStencilStateName: materialAsset.DepthStencilStateName);
        }

        bool transparencyFromProperties = HasTransparencyValue(definition, effectiveValues);
        bool transparencyFromBlend = IsTransparentBlend(materialAsset.BlendStateName);
        bool isTransparent = materialAsset.IsTransparent
            || materialAsset.Queue >= RenderQueue.Transparent
            || transparencyFromBlend
            || transparencyFromProperties;

        var queue = materialAsset.Queue;
        if (isTransparent && queue < RenderQueue.Transparent)
        {
            queue = RenderQueue.Transparent;
        }

        string blendStateName = materialAsset.BlendStateName;
        if (isTransparent && string.Equals(blendStateName, MaterialAsset.DefaultBlendStateName, StringComparison.OrdinalIgnoreCase))
        {
            blendStateName = "AlphaBlend";
        }

        string depthStencilStateName = materialAsset.DepthStencilStateName;
        if (queue >= RenderQueue.Transparent
            && string.Equals(depthStencilStateName, MaterialAsset.DefaultDepthStencilStateName, StringComparison.OrdinalIgnoreCase))
        {
            depthStencilStateName = "Read";
        }

        return new ResolvedMaterialRenderState(isTransparent, queue, blendStateName, depthStencilStateName);
    }

    private static bool HasTransparencyValue(
        MaterialDefinition definition,
        IReadOnlyDictionary<string, MaterialValue> effectiveValues)
    {
        for (int i = 0; i < definition.Properties.Count; i++)
        {
            var propertyDefinition = definition.Properties[i];
            if ((propertyDefinition.Flags & MaterialPropertyFlags.AffectsTransparency) == 0
                || !effectiveValues.TryGetValue(propertyDefinition.Key, out var value))
            {
                continue;
            }

            switch (propertyDefinition.ValueType)
            {
                case MaterialPropertyType.Float when value.TryGetFloat(out var floatValue):
                    if (floatValue < 0.999f)
                    {
                        return true;
                    }

                    break;

                case MaterialPropertyType.Color when value.TryGetColor(out var colorValue):
                    if (colorValue.A < byte.MaxValue)
                    {
                        return true;
                    }

                    break;

                case MaterialPropertyType.Vector4 when value.TryGetVector4(out var vector4Value):
                    if (vector4Value.W < 0.999f)
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    private static bool IsTransparentBlend(string blendStateName)
        => !string.Equals(blendStateName, MaterialAsset.DefaultBlendStateName, StringComparison.OrdinalIgnoreCase);
}