using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Rendering.Models;

using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Draw;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Shadows;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Application.Components;

public class SkinnedMeshRendererComponent : DrawableGameComponent, IViewFlushableRenderer
{
    private readonly List<SkinnedMeshInfo> _meshInfos = new();
    private readonly RenderStateCache _stateCache = new();
    private readonly ShaderBindCache _shaderCache = new();
    private readonly LitDiffuseMaterial _defaultMaterial = new()
    {
        // Preserve the historical skinned blend mode until rigged meshes carry authored materials.
        BlendState = BlendState.NonPremultiplied,
        DepthStencilState = DepthStencilState.Default,
        RasterizerState = RasterizerState.CullCounterClockwise,
        SamplerState = SamplerState.AnisotropicClamp,
        DiffuseColor = Color.White,
        EmissiveColor = Vector3.Zero,
        SpecularColor = new Vector3(0.3f, 0.3f, 0.3f),
        SpecularPower = 16.0f,
    };
    private Effect _effect = null!;
    private ShaderWrapper _shader = null!;
    private ShaderManager _shaderManager;
    private ShaderVariantLibrary _variantLibrary;
    private RenderShaderSelector _shaderSelector;
    private StaticMeshRendererComponent _staticMeshRendererComponent;

    /// <summary>
    /// Fallback lighting for direct renderer usage when no frame lighting is supplied.
    /// The default instance is empty to avoid implicit hardcoded scene lighting.
    /// </summary>
    public LightingContext DefaultLighting { get; } = new();

    public SkinnedMeshRendererComponent(CasaEngineGame game) : base(game)
    {
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.MeshComponent;
        DrawOrder = (int)ComponentDrawOrder.MeshComponent;
        DefaultLighting.AmbientColor = Vector3.Zero;
    }

    public void AddMesh(
        RiggedModel mesh,
        Matrix world,
        ISkinnedMeshPoseProvider poseProvider,
        SkinningModeSelection skinningModeSelection,
        bool castShadows = true,
        bool receiveShadows = true)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(poseProvider);

        _meshInfos.Add(new SkinnedMeshInfo
        {
            SkinnedMesh = mesh,
            PoseProvider = poseProvider,
            SkinningModeSelection = skinningModeSelection,
            World = world,
            CastShadows = castShadows,
            ReceiveShadows = receiveShadows,
        });
    }

    protected override void LoadContent()
    {
        var linearBlendShader = SkinningModeShaderResolver.Resolve(SkinningMode.LinearBlend);
        var dualQuaternionShader = SkinningModeShaderResolver.Resolve(SkinningMode.DualQuaternion);
        _effect = Game.Content.Load<Effect>(BuiltInShaderCatalog.SkinEffectContentName);
        _shader = new ShaderWrapper(_effect);

        if (Game is CasaEngineGame casaEngineGame)
        {
            _shaderManager = new ShaderManager(casaEngineGame.AssetContentManager);
            _variantLibrary = new ShaderVariantLibrary(_shaderManager);
            _shaderManager.RegisterShader(linearBlendShader.ShaderId, _shader);
            _variantLibrary.RegisterTechniqueAliases(linearBlendShader.ShaderId, ShaderVariantLibrary.BuildSkinnedEffectAliases());
            _shaderManager.RegisterShader(dualQuaternionShader.ShaderId, _shader);
            _variantLibrary.RegisterTechniqueAliases(dualQuaternionShader.ShaderId, ShaderVariantLibrary.BuildDualQuaternionSkinnedEffectAliases());
            _staticMeshRendererComponent = casaEngineGame.MeshRendererComponent;
        }

        _shaderSelector = new RenderShaderSelector(_shader, _shaderManager, _variantLibrary);
        _shaderSelector.RegisterShader(linearBlendShader.ShaderId, _shader);
        _shaderSelector.RegisterShader(dualQuaternionShader.ShaderId, _shader);

        // Provide a 1×1 white fallback texture for skinned meshes without textures.
        if (RiggedModelLoader.DefaultTexture == null)
        {
            var white = new Texture2D(Game.GraphicsDevice, 1, 1);
            white.SetData(new[] { Color.White });
            RiggedModelLoader.DefaultTexture = white;
        }

        base.LoadContent();
    }

    public bool TryReloadBuiltInShader(string contentName, Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        if (_shader is null)
        {
            return false;
        }

        if (!string.Equals(
                BuiltInShaderCatalog.NormalizeContentName(contentName),
                BuiltInShaderCatalog.SkinEffectContentName,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _shader.ReplaceEffect(effect);
        _effect = effect;
        return true;
    }

    /// <inheritdoc/>
    public void Flush(in RenderFrame frame, RenderStats stats = null)
    {
        if (_meshInfos.Count == 0)
        {
            return;
        }

        if (_shaderSelector is null)
        {
            return;
        }

        _stateCache.ResetFrame();
        _shaderCache.ResetFrame();

        stats ??= new RenderStats();
        GraphicsDevice graphicsDevice = GraphicsDevice;
        GraphicsDevice.SetVertexBuffer(null);
        GraphicsDevice.Indices = null;

        var context = new RenderContext
        {
            Device = graphicsDevice,
            Frame = frame,
            Lighting = frame.Lighting ?? DefaultLighting,
            Environment = frame.Environment,
            Shadows = _staticMeshRendererComponent?.ShadowResources,
            Stats = stats,
        };

        DrawShadowCasters(in context);

        foreach (var meshInfo in _meshInfos)
        {
            if (meshInfo.SkinnedMesh == null)
            {
                continue;
            }

            if (meshInfo.PoseProvider == null)
            {
                continue;
            }

            SkinningMode effectiveSkinningMode = SkinningModeSelectionResolver.ResolveEffective(
                meshInfo.SkinningModeSelection,
                meshInfo.SkinnedMesh.SkinningMode,
                meshInfo.PoseProvider.CanUseDualQuaternionSkinning);

            DrawRiggedModel(
                meshInfo.SkinnedMesh,
                meshInfo.World,
                meshInfo.PoseProvider,
                effectiveSkinningMode,
                meshInfo.ReceiveShadows,
                in context);
        }

        _meshInfos.Clear();
    }

    private void DrawShadowCasters(in RenderContext context)
    {
        if (context.Shadows is null
            || context.Shadows.ShadowMapAtlas is null
            || context.Shadows.VisibleLightCount <= 0)
        {
            return;
        }

        ShadowLight shadowLight = context.Shadows.VisibleLights[0];
        if (shadowLight.Type != ShadowLightType.Directional)
        {
            return;
        }

        using var guard = new GraphicsStateGuard(context.Device);

        Rectangle viewport = shadowLight.AtlasViewport;
        context.Device.SetRenderTarget(context.Shadows.ShadowMapAtlas);
        context.Device.Viewport = new Viewport(viewport.X, viewport.Y, viewport.Width, viewport.Height);
        context.Device.BlendState = BlendState.Opaque;
        context.Device.DepthStencilState = DepthStencilState.Default;
        context.Device.RasterizerState = RasterizerState.CullCounterClockwise;
        context.Device.SetVertexBuffer(null);
        context.Device.Indices = null;

        if (context.Stats is not null)
        {
            context.Stats.EffectBinds++;
        }

        foreach (var meshInfo in _meshInfos)
        {
            if (!meshInfo.CastShadows || meshInfo.SkinnedMesh == null || meshInfo.PoseProvider == null)
            {
                continue;
            }

            SkinningMode effectiveSkinningMode = SkinningModeSelectionResolver.ResolveEffective(
                meshInfo.SkinningModeSelection,
                meshInfo.SkinnedMesh.SkinningMode,
                meshInfo.PoseProvider.CanUseDualQuaternionSkinning);

            DrawRiggedModelShadowCasters(
                meshInfo.SkinnedMesh,
                meshInfo.World,
                meshInfo.PoseProvider,
                effectiveSkinningMode,
                shadowLight,
                in context);
        }
    }

    private void DrawRiggedModel(
        RiggedModel riggedModel,
        Matrix world,
        ISkinnedMeshPoseProvider poseProvider,
        SkinningMode skinningMode,
        bool receiveShadows,
        in RenderContext context)
    {
        for (int meshIndex = 0; meshIndex < riggedModel.Meshes.Length; meshIndex++)
        {
            var mesh = riggedModel.Meshes[meshIndex];
            var texture = mesh.Texture;
            if (texture == null)
            {
                continue;
            }

            DrawRiggedMesh(riggedModel, mesh, texture, world, poseProvider, skinningMode, receiveShadows, in context);
        }
    }

    private void DrawRiggedModelShadowCasters(
        RiggedModel riggedModel,
        Matrix world,
        ISkinnedMeshPoseProvider poseProvider,
        SkinningMode skinningMode,
        ShadowLight shadowLight,
        in RenderContext context)
    {
        for (int meshIndex = 0; meshIndex < riggedModel.Meshes.Length; meshIndex++)
        {
            DrawRiggedMeshShadowCaster(
                riggedModel,
                riggedModel.Meshes[meshIndex],
                world,
                poseProvider,
                skinningMode,
                shadowLight,
                in context);
        }
    }

    private void DrawRiggedMesh(
        RiggedModel riggedModel,
        RiggedModel.RiggedModelMesh mesh,
        Texture2D texture,
        Matrix world,
        ISkinnedMeshPoseProvider poseProvider,
        SkinningMode skinningMode,
        bool receiveShadows,
        in RenderContext context)
    {
        var vertexDeclaration = SkinningModeShaderResolver.ResolveVertexDeclaration(skinningMode);
        mesh.Initialize(context.Device, vertexDeclaration);
        if (mesh.IndexBuffer == null)
        {
            return;
        }

        var vertexBuffer = poseProvider.GetVertexBufferOverride(mesh, context.Device, vertexDeclaration) ?? mesh.VertexBuffer;
        if (vertexBuffer == null)
        {
            return;
        }

        _defaultMaterial.BasColor = texture;

        var features = RenderFeatureResolver.ResolveSkinned(_defaultMaterial, mesh);
        var effectiveShader = EffectiveShaderResolver.Resolve(_defaultMaterial, features, skinningMode);
        var resolvedShader = _shaderSelector!.Resolve(effectiveShader.ShaderId, features);
        var meshWorld = world * poseProvider.GetMeshNodeTransform(mesh);

        _stateCache.Apply(context.Device, _defaultMaterial, context.Stats);

        if (!resolvedShader.TechniqueSelectedBySelector)
        {
            _defaultMaterial.SelectTechnique(resolvedShader.Shader, in context, features);
        }

        _shaderCache.BindGlobals(resolvedShader.Shader, in context);
        _defaultMaterial.Bind(resolvedShader.Shader, in context, meshWorld);
        resolvedShader.Shader.SetParameter(ShaderParameterNames.ReceiveShadows, receiveShadows ? 1.0f : 0.0f);

        if (skinningMode == SkinningMode.DualQuaternion && resolvedShader.Shader.HasParameter(ShaderParameterNames.BonesDualQuaternion))
        {
            resolvedShader.Shader.SetParameter(ShaderParameterNames.BonesDualQuaternion, poseProvider.DualQuaternionSkinningPalette);
        }
        else
        {
            resolvedShader.Shader.SetParameter(ShaderParameterNames.Bones, poseProvider.SkinningPalette);
        }

        context.Device.SetVertexBuffer(vertexBuffer);
        context.Device.Indices = mesh.IndexBuffer;

        for (int passIndex = 0; passIndex < resolvedShader.Shader.PassCount; passIndex++)
        {
            resolvedShader.Shader.ApplyPass(passIndex);
            context.Device.DrawIndexedPrimitives(
                PrimitiveType.TriangleList,
                0,
                0,
                mesh.NumberOfVertices,
                0,
                mesh.NumberOfIndices / 3);
        }

        if (context.Stats is not null)
        {
            context.Stats.DrawCalls++;
        }
    }

    private void DrawRiggedMeshShadowCaster(
        RiggedModel riggedModel,
        RiggedModel.RiggedModelMesh mesh,
        Matrix world,
        ISkinnedMeshPoseProvider poseProvider,
        SkinningMode skinningMode,
        ShadowLight shadowLight,
        in RenderContext context)
    {
        var vertexDeclaration = SkinningModeShaderResolver.ResolveVertexDeclaration(skinningMode);
        mesh.Initialize(context.Device, vertexDeclaration);
        if (mesh.IndexBuffer == null)
        {
            return;
        }

        var vertexBuffer = poseProvider.GetVertexBufferOverride(mesh, context.Device, vertexDeclaration) ?? mesh.VertexBuffer;
        if (vertexBuffer == null)
        {
            return;
        }

        Matrix meshWorld = world * poseProvider.GetMeshNodeTransform(mesh);

        _shader.SelectTechnique(skinningMode == SkinningMode.DualQuaternion
            ? "RiggedModelShadowDepthDualQuaternion"
            : "RiggedModelShadowDepth");
        _shader.SetParameter(ShaderParameterNames.WorldViewProj, meshWorld * shadowLight.LightViewProjection);

        if (skinningMode == SkinningMode.DualQuaternion && _shader.HasParameter(ShaderParameterNames.BonesDualQuaternion))
        {
            _shader.SetParameter(ShaderParameterNames.BonesDualQuaternion, poseProvider.DualQuaternionSkinningPalette);
        }
        else
        {
            _shader.SetParameter(ShaderParameterNames.Bones, poseProvider.SkinningPalette);
        }

        context.Device.SetVertexBuffer(vertexBuffer);
        context.Device.Indices = mesh.IndexBuffer;

        for (int passIndex = 0; passIndex < _shader.PassCount; passIndex++)
        {
            _shader.ApplyPass(passIndex);
            context.Device.DrawIndexedPrimitives(
                PrimitiveType.TriangleList,
                0,
                0,
                mesh.NumberOfVertices,
                0,
                mesh.NumberOfIndices / 3);
        }

        if (context.Stats is not null)
        {
            context.Stats.DrawCalls++;
        }
    }

    private class SkinnedMeshInfo
    {
        public RiggedModel SkinnedMesh;
        public ISkinnedMeshPoseProvider PoseProvider;
        public SkinningModeSelection SkinningModeSelection { get; set; }
        public Matrix World { get; set; }
        public bool CastShadows { get; set; } = true;
        public bool ReceiveShadows { get; set; } = true;
    }
}
