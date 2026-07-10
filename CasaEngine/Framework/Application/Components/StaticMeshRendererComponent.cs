using CasaEngine.Framework.Rendering.Models;

using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Draw;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Shadows;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Application.Components;

public class StaticMeshRendererComponent : DrawableGameComponent, IViewFlushableRenderer
{
    private readonly List<MeshInfo> _meshInfos = new();
    private Effect _effect;
    private Effect _skyEffect;
    private ShaderWrapper _legacyShaderWrapper;
    private ShaderWrapper _unlitShaderWrapper;
    private ShaderWrapper _shadowDepthShaderWrapper;
    private SkyCubemapRenderer _skyRenderer;
    private MaterialCache _materialCache;

    // Phase 4 — per-frame caches that minimise redundant state/shader changes
    private readonly RenderStateCache _stateCache   = new();
    private readonly ShaderBindCache  _shaderCache  = new();
    private readonly List<RenderItem> _renderItems  = new();

    // Phase 7 — shader variant system
    private ShaderManager         _shaderManager;
    private ShaderVariantLibrary  _variantLibrary;
    private RenderShaderSelector  _shaderSelector;

    // Phase 9 — hardware instancing
    private InstanceBatcher _instanceBatcher;

    // Phase 10 — forward render pipeline
    private readonly ForwardRenderPipeline _pipeline = new();
    private readonly ForwardShadowResources _shadowResources = new();

    /// <summary>
    /// Fallback lighting used only when a caller flushes the renderer without a populated <see cref="RenderFrame.Lighting"/>.
    /// The default instance is intentionally empty so the runtime does not hide missing scene lights behind hardcoded values.
    /// </summary>
    public LightingContext DefaultLighting { get; } = new();

    public ForwardShadowResources ShadowResources => _shadowResources;

    /// <summary>
    /// Registers a callback that is invoked after the shadow pass but before the
    /// opaque pass. Used by the skinned-mesh renderer to inject its shadow casters
    /// into the atlas while static scene geometry has not yet been drawn.
    /// </summary>
    public void RegisterPostShadowCallback(Action<RenderContext> callback)
        => _pipeline.PostShadowPassCallback = callback;

    public StaticMeshRendererComponent(Game game) : base(game)
    {
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.MeshComponent;
        DrawOrder = (int)ComponentDrawOrder.MeshComponent;
        DefaultLighting.AmbientColor = Vector3.Zero;
    }

    /// <summary>Enqueue a <see cref="StaticModelMesh"/> sub-mesh for rendering.</summary>
    /// <param name="propertyOverrides">
    /// Optional per-instance parameter overrides applied after the material's Bind() call.
    /// Allows per-entity colour tint, highlight etc. without duplicating the material asset.
    /// </param>
    public void AddMesh(
        StaticModelMesh staticModelMesh,
        Matrix world,
        Matrix worldInvertTranspose,
        MaterialPropertyBlock propertyOverrides = null,
        bool castShadows = true,
        bool receiveShadows = true)
    {
        AddMesh(staticModelMesh, world, worldInvertTranspose, null, propertyOverrides, null, castShadows, receiveShadows);
    }

    public void AddMesh(
        StaticModelMesh staticModelMesh,
        Matrix world,
        Matrix worldInvertTranspose,
        IReadOnlyDictionary<int, MaterialBase> materialOverridesBySlotIndex,
        MaterialPropertyBlock propertyOverrides = null,
        IReadOnlyDictionary<int, MaterialPropertyBlock> propertyOverridesBySlotIndex = null,
        bool castShadows = true,
        bool receiveShadows = true)
    {
        _meshInfos.Add(new MeshInfo
        {
            StaticModelMesh  = staticModelMesh,
            World            = world,
            WorldInvertTranspose = worldInvertTranspose,
            Material         = staticModelMesh.Material,
            MaterialOverridesBySlotIndex = materialOverridesBySlotIndex,
            PropertyOverrides = propertyOverrides,
            PropertyOverridesBySlotIndex = propertyOverridesBySlotIndex,
            CastShadows = castShadows,
            ReceiveShadows = receiveShadows,
        });
    }

    protected override void LoadContent()
    {
        _effect = Game.Content.Load<Effect>(BuiltInShaderCatalog.LitForwardContentName);
        var unlitEffect = Game.Content.Load<Effect>(BuiltInShaderCatalog.UnlitTextureContentName);
        var shadowDepthEffect = Game.Content.Load<Effect>(BuiltInShaderCatalog.ShadowDepthContentName);
        _skyEffect = Game.Content.Load<Effect>(BuiltInShaderCatalog.SkyCubemapContentName);
        _skyRenderer = new SkyCubemapRenderer(_skyEffect);
        _shadowDepthShaderWrapper = new ShaderWrapper(shadowDepthEffect);
        _pipeline.SetShadowShader(_shadowDepthShaderWrapper);
        _pipeline.SetSkyRenderer(_skyRenderer);

        ApplyLitForwardDefaults(_effect);

        _legacyShaderWrapper = new ShaderWrapper(_effect);
        _unlitShaderWrapper = new ShaderWrapper(unlitEffect);

        // Phase 7: initialise shader variant system
        var acm = (Game as CasaEngineGame)?.AssetContentManager;
        if (acm is not null)
        {
            _materialCache = acm.RuntimeContext?.MaterialCache;
            _shaderManager  = new ShaderManager(acm);
            _variantLibrary = new ShaderVariantLibrary(_shaderManager);
            _shaderManager.RegisterShader(EffectiveShaderResolver.LitForwardShaderId, _legacyShaderWrapper);
            _shaderManager.RegisterShader(EffectiveShaderResolver.ReflectiveLitForwardShaderId, _legacyShaderWrapper);
            _shaderManager.RegisterShader(EffectiveShaderResolver.UnlitTextureShaderId, _unlitShaderWrapper);
            _variantLibrary.RegisterTechniqueAliases(EffectiveShaderResolver.LitForwardShaderId, ShaderVariantLibrary.BuildLitForwardAliases());
            _variantLibrary.RegisterTechniqueAliases(EffectiveShaderResolver.ReflectiveLitForwardShaderId, ShaderVariantLibrary.BuildLitForwardAliases());
            _variantLibrary.RegisterTechniqueAliases(EffectiveShaderResolver.UnlitTextureShaderId, ShaderVariantLibrary.BuildUnlitTextureAliases());
        }

        _shaderSelector = new RenderShaderSelector(_legacyShaderWrapper, _shaderManager, _variantLibrary);
        _shaderSelector.RegisterShader(EffectiveShaderResolver.LitForwardShaderId, _legacyShaderWrapper);
        _shaderSelector.RegisterShader(EffectiveShaderResolver.ReflectiveLitForwardShaderId, _legacyShaderWrapper);
        _shaderSelector.RegisterShader(EffectiveShaderResolver.UnlitTextureShaderId, _unlitShaderWrapper);

        // Phase 9: hardware instancing batcher
        _instanceBatcher = new InstanceBatcher(_effect.GraphicsDevice);

        base.LoadContent();
    }

    public bool TryReloadBuiltInShader(string contentName, Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        if (_legacyShaderWrapper is null
            || _unlitShaderWrapper is null
            || _shadowDepthShaderWrapper is null
            || _skyRenderer is null)
        {
            return false;
        }

        string normalizedContentName = BuiltInShaderCatalog.NormalizeContentName(contentName);
        if (string.Equals(normalizedContentName, BuiltInShaderCatalog.LitForwardContentName, StringComparison.OrdinalIgnoreCase))
        {
            ApplyLitForwardDefaults(effect);
            _legacyShaderWrapper.ReplaceEffect(effect);
            _effect = effect;
            return true;
        }

        if (string.Equals(normalizedContentName, BuiltInShaderCatalog.UnlitTextureContentName, StringComparison.OrdinalIgnoreCase))
        {
            _unlitShaderWrapper.ReplaceEffect(effect);
            return true;
        }

        if (string.Equals(normalizedContentName, BuiltInShaderCatalog.ShadowDepthContentName, StringComparison.OrdinalIgnoreCase))
        {
            _shadowDepthShaderWrapper.ReplaceEffect(effect);
            return true;
        }

        if (string.Equals(normalizedContentName, BuiltInShaderCatalog.SkyCubemapContentName, StringComparison.OrdinalIgnoreCase))
        {
            Effect previousSkyEffect = _skyEffect;
            _skyEffect = effect;
            _skyRenderer = new SkyCubemapRenderer(_skyEffect);
            _pipeline.SetSkyRenderer(_skyRenderer);
            previousSkyEffect.Dispose();
            return true;
        }

        return false;
    }

    private static void ApplyLitForwardDefaults(Effect effect)
    {
        var technique = effect.Techniques["LitForward_PixelLighting_Texture"];
        if (technique != null)
        {
            effect.CurrentTechnique = technique;
        }

        effect.Parameters["DiffuseColor"]?.SetValue(Vector4.One);
        effect.Parameters["EmissiveColor"]?.SetValue(Vector3.One * 0.5f);
        effect.Parameters["SpecularColor"]?.SetValue(Vector3.One * 0.5f);
        effect.Parameters["SpecularPower"]?.SetValue(5.0f);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// WorldViewProj and EyePosition are computed from <paramref name="frame"/> at flush time,
    /// so each view correctly uses its own camera data.
    /// </remarks>
    public void Flush(in RenderFrame frame, RenderStats stats = null)
    {
        GraphicsDevice graphicsDevice = _effect.GraphicsDevice;

        // Reset per-frame state caches
        _stateCache.ResetFrame();
        _shaderCache.ResetFrame();

        stats ??= new RenderStats();
        var context = new RenderContext
        {
            Device   = graphicsDevice,
            Frame    = frame,
            Lighting = frame.Lighting ?? DefaultLighting,
            Environment = frame.Environment,
            Shadows = _shadowResources,
            Stats    = stats,
        };

        ApplyShadowSettings(frame.Shadows, _shadowResources.Settings);
        _shadowResources.Clear();

        // --- Phase 4: build a sorted RenderItem list ---
        _renderItems.Clear();

        foreach (var meshInfo in _meshInfos)
        {
            if (meshInfo.StaticModelMesh == null)
            {
                continue;
            }

            var vb = meshInfo.StaticModelMesh.VertexBuffer;
            var ib = meshInfo.StaticModelMesh.IndexBuffer;
            if (vb == null || ib == null)
            {
                continue;
            }

            var mesh = meshInfo.StaticModelMesh;

            if (mesh.SubMeshes.Count > 0)
            {
                foreach (var subMesh in mesh.SubMeshes)
                {
                    var mat = GetMaterialOverride(meshInfo.MaterialOverridesBySlotIndex, subMesh.MaterialSlotIndex)
                        ?? subMesh.Material
                        ?? meshInfo.Material;
                    if (mat == null)
                    {
                        continue;
                    }

                    var propertyOverrides = GetPropertyOverride(meshInfo.PropertyOverridesBySlotIndex, subMesh.MaterialSlotIndex)
                        ?? meshInfo.PropertyOverrides;

                    float dist = Vector3.Distance(meshInfo.World.Translation, frame.CameraPosition);
                    var compiledMaterial = ResolveCompiledMaterial(mat);
                    var item = new RenderItem
                    {
                        Mesh                  = mesh,
                        SubMesh               = subMesh,
                        Material              = mat,
                        CompiledMaterial      = compiledMaterial,
                        ComponentCastShadows  = meshInfo.CastShadows,
                        ComponentReceiveShadows = meshInfo.ReceiveShadows,
                        EffectiveShaderId     = compiledMaterial?.EffectiveShader.ShaderId ?? EffectiveShaderResolver.Resolve(mat).ShaderId,
                        World                 = meshInfo.World,
                        WorldInverseTranspose = meshInfo.WorldInvertTranspose,
                        DistanceToCamera      = dist,
                        PropertyOverrides     = propertyOverrides,
                        Features              = compiledMaterial?.Features ?? ResolveRenderFeatures(mat, mesh),
                    };
                    item.SortKey = SortKeyGenerator.Generate(
                        item.Queue,
                        item.EffectiveShaderId.GetHashCode(),
                        mat.Id.GetHashCode(),
                        vb.GetHashCode(),
                        dist);
                    _renderItems.Add(item);
                }
            }
            else
            {
                var mat = GetMaterialOverride(meshInfo.MaterialOverridesBySlotIndex, mesh.MaterialSlotIndex)
                    ?? meshInfo.Material;
                if (mat == null)
                {
                    continue;
                }

                var propertyOverrides = GetPropertyOverride(meshInfo.PropertyOverridesBySlotIndex, mesh.MaterialSlotIndex)
                    ?? meshInfo.PropertyOverrides;

                float dist = Vector3.Distance(meshInfo.World.Translation, frame.CameraPosition);
                var compiledMaterial = ResolveCompiledMaterial(mat);
                var item = new RenderItem
                {
                    Mesh                  = mesh,
                    SubMesh               = null,
                    Material              = mat,
                    CompiledMaterial      = compiledMaterial,
                    ComponentCastShadows  = meshInfo.CastShadows,
                    ComponentReceiveShadows = meshInfo.ReceiveShadows,
                    EffectiveShaderId     = compiledMaterial?.EffectiveShader.ShaderId ?? EffectiveShaderResolver.Resolve(mat).ShaderId,
                    World                 = meshInfo.World,
                    WorldInverseTranspose = meshInfo.WorldInvertTranspose,
                    DistanceToCamera      = dist,
                    PropertyOverrides     = propertyOverrides,
                    Features              = compiledMaterial?.Features ?? ResolveRenderFeatures(mat, mesh),
                };
                item.SortKey = SortKeyGenerator.Generate(
                    item.Queue,
                    item.EffectiveShaderId.GetHashCode(),
                    mat.Id.GetHashCode(),
                    vb.GetHashCode(),
                    dist);
                _renderItems.Add(item);
            }
        }

        // Sort by state (queue/shader/material/mesh); for transparent items the SortKey
        // also encodes reversed distance so they end up back-to-front within a state group.
        _renderItems.Sort(static (a, b) => a.SortKey.CompareTo(b.SortKey));

        // --- Phase 9: hardware-instanced draw for groups with ShaderFeature.Instanced ---
        // Items that go through instancing are removed from the regular draw list.
        if (_instanceBatcher is not null)
        {
            // Group by (VertexBuffer ptr, SortKey of first element) — same mesh + material
            var instanceGroups = new Dictionary<(IntPtr, ulong, bool), List<RenderItem>>();

            for (int i = 0; i < _renderItems.Count; i++)
            {
                var item = _renderItems[i];
                if ((item.Features & ShaderFeature.Instanced) == 0)
                {
                    continue;
                }

                var groupKey = (item.Mesh.VertexBuffer!.Tag as IntPtr? ?? IntPtr.Zero, item.SortKey & ~0xFFFUL, item.EffectiveReceiveShadows);
                if (!instanceGroups.TryGetValue(groupKey, out var list))
                {
                    list = new List<RenderItem>();
                    instanceGroups[groupKey] = list;
                }
                list.Add(item);
            }

            // Draw groups that exceed the threshold; put the rest back on the regular list
            foreach (var group in instanceGroups.Values)
            {
                if (group.Count < _instanceBatcher.MinInstanceThreshold)
                {
                    continue; // too small → handled by regular path
                }

                var firstItem = group[0];
                if (firstItem.Material.Queue >= RenderQueue.Transparent)
                {
                    stats.TransparentItems += group.Count;
                }
                else
                {
                    stats.OpaqueItems += group.Count;
                }

                _stateCache.Apply(graphicsDevice, firstItem, stats);
                var resolvedShader = _shaderSelector!.Resolve(in firstItem);
                _shaderCache.BindGlobals(resolvedShader.Shader, in context);
                _instanceBatcher.DrawInstancedGroup(group, resolvedShader.Shader, in context, resolvedShader.TechniqueSelectedBySelector);
                stats.DrawCalls++;
            }
        }

        // --- Phase 10: delegate sorted items to ForwardRenderPipeline ---
        _pipeline.Render(context, _renderItems, _stateCache, _shaderCache, _shaderSelector!);

        _meshInfos.Clear();
    }

    private static void ApplyShadowSettings(ShadowSettings source, ShadowSettings destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (source is null)
        {
            destination.ResetToDefaults();
            return;
        }

        destination.CopyFrom(source);
    }

    private static MaterialBase GetMaterialOverride(IReadOnlyDictionary<int, MaterialBase> materialOverridesBySlotIndex, int slotIndex)
    {
        if (materialOverridesBySlotIndex == null || slotIndex < 0)
        {
            return null;
        }

        return materialOverridesBySlotIndex.TryGetValue(slotIndex, out var materialOverride)
            ? materialOverride
            : null;
    }

    private static MaterialPropertyBlock GetPropertyOverride(IReadOnlyDictionary<int, MaterialPropertyBlock> propertyOverridesBySlotIndex, int slotIndex)
    {
        if (propertyOverridesBySlotIndex == null || slotIndex < 0)
        {
            return null;
        }

        return propertyOverridesBySlotIndex.TryGetValue(slotIndex, out var propertyOverride)
            ? propertyOverride
            : null;
    }

    private static ShaderFeature ResolveRenderFeatures(MaterialBase material, StaticModelMesh mesh)
        => RenderFeatureResolver.Resolve(new RenderFeatureInput
        {
            Material = material,
            Mesh = mesh,
        });

    private CompiledMaterial ResolveCompiledMaterial(MaterialBase material)
    {
        if (_materialCache == null || material.Id == Guid.Empty)
        {
            return null;
        }

        return _materialCache.TryGet(material.Id, out var compiledMaterial)
            ? compiledMaterial
            : null;
    }

    private class MeshInfo
    {
        public StaticModelMesh StaticModelMesh;
        public Matrix World;
        public Matrix WorldInvertTranspose;
        public MaterialBase Material;
        public IReadOnlyDictionary<int, MaterialBase> MaterialOverridesBySlotIndex;
        /// <summary>Optional per-instance overrides (Phase 6).</summary>
        public MaterialPropertyBlock PropertyOverrides;
        public IReadOnlyDictionary<int, MaterialPropertyBlock> PropertyOverridesBySlotIndex;
        public bool CastShadows = true;
        public bool ReceiveShadows = true;
    }
}
