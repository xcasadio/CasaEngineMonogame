using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Draw;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DirLight = CasaEngine.Framework.Rendering.DirectionalLight;

namespace CasaEngine.Framework.Game.Components;

public class StaticMeshRendererComponent : DrawableGameComponent, IViewFlushableRenderer
{
    private readonly List<MeshInfo> _meshInfos = new();
    private Effect _effect;
    private ShaderWrapper? _legacyShaderWrapper;

    // Phase 4 — per-frame caches that minimise redundant state/shader changes
    private readonly RenderStateCache _stateCache   = new();
    private readonly ShaderBindCache  _shaderCache  = new();
    private readonly List<RenderItem> _renderItems  = new();

    // Phase 7 — shader variant system
    private ShaderManager?         _shaderManager;
    private ShaderVariantLibrary?  _variantLibrary;

    // Phase 9 — hardware instancing
    private InstanceBatcher? _instanceBatcher;

    // Phase 10 — forward render pipeline
    private readonly ForwardRenderPipeline _pipeline = new();

    /// <summary>
    /// Default scene lighting used when no external <see cref="LightingContext"/> is supplied.
    /// Values mirror the three-directional-light setup previously hardcoded in LoadContent.
    /// Replace at runtime to change scene lighting (Phase 5).
    /// </summary>
    public LightingContext DefaultLighting { get; } = new()
    {
        ActiveDirectionalLightCount = 3,
        AmbientColor = new Vector3(0.05f, 0.05f, 0.05f),
    };

    public StaticMeshRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.MeshComponent;
        DrawOrder = (int)ComponentDrawOrder.MeshComponent;
    }

    /// <summary>Enqueue a <see cref="StaticModelMesh"/> sub-mesh for rendering.</summary>
    /// <param name="propertyOverrides">
    /// Optional per-instance parameter overrides applied after the material's Bind() call.
    /// Allows per-entity colour tint, highlight etc. without duplicating the material asset.
    /// </param>
    public void AddMesh(StaticModelMesh staticModelMesh, Matrix world, Matrix worldInvertTranspose,
        MaterialPropertyBlock? propertyOverrides = null)
    {
        _meshInfos.Add(new MeshInfo
        {
            StaticModelMesh  = staticModelMesh,
            World            = world,
            WorldInvertTranspose = worldInvertTranspose,
            Material         = staticModelMesh.Material,
            PropertyOverrides = propertyOverrides,
        });
    }

    protected override void LoadContent()
    {
        _effect = Game.Content.Load<Effect>("Shaders\\basicEffect");
        _effect.CurrentTechnique = _effect.Techniques["BasicEffect_PixelLighting_Texture"];

        _effect.Parameters["DiffuseColor"].SetValue(Vector4.One);
        _effect.Parameters["EmissiveColor"].SetValue(Vector3.One * 0.5f);
        _effect.Parameters["SpecularColor"].SetValue(Vector3.One * 0.5f);
        _effect.Parameters["SpecularPower"].SetValue(5.0f);

        // Initialise default lighting context to match the previous hardcoded values.
        // External code can modify DefaultLighting to change scene illumination.
        // Neutral 3-point lights: direction preserved, colours balanced to equal RGB
        // so that a white DiffuseColor renders as white rather than warm/yellow.
        DefaultLighting.DirectionalLights[0] = new DirLight(
            new Vector3(-0.5265408f, -0.5735765f, -0.6275069f),
            new Vector3(0.92f, 0.92f, 0.92f),
            new Vector3(0.92f, 0.92f, 0.92f));
        DefaultLighting.DirectionalLights[1] = new DirLight(
            new Vector3(0.7198464f, 0.3420201f, 0.6040227f),
            new Vector3(0.71f, 0.71f, 0.71f),
            Vector3.Zero);
        DefaultLighting.DirectionalLights[2] = new DirLight(
            new Vector3(0.4545195f, -0.7660444f, 0.4545195f),
            new Vector3(0.36f, 0.36f, 0.36f),
            new Vector3(0.36f, 0.36f, 0.36f));

        _legacyShaderWrapper = new ShaderWrapper(_effect);

        // Phase 7: initialise shader variant system
        var acm = (Game as CasaEngineGame)?.AssetContentManager;
        if (acm is not null)
        {
            _shaderManager  = new ShaderManager(acm);
            _variantLibrary = new ShaderVariantLibrary(_shaderManager);
        }

        // Phase 9: hardware instancing batcher
        _instanceBatcher = new InstanceBatcher(_effect.GraphicsDevice);

        base.LoadContent();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// WorldViewProj and EyePosition are computed from <paramref name="frame"/> at flush time,
    /// so each view correctly uses its own camera data.
    /// </remarks>
    public void Flush(in RenderFrame frame)
    {
        GraphicsDevice graphicsDevice = _effect.GraphicsDevice;

        var defaultTexture = (Game as CasaEngineGame)?.AssetContentManager
            .GetAsset<Assets.Textures.Texture>(Assets.Textures.Texture.DefaultTextureName);

        // Reset per-frame state caches
        _stateCache.ResetFrame();
        _shaderCache.ResetFrame();

        var stats   = new RenderStats();
        var context = new RenderContext
        {
            Device   = graphicsDevice,
            Frame    = frame,
            Lighting = DefaultLighting,
            Stats    = stats,
        };

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
                    var mat = subMesh.Material ?? meshInfo.Material;
                    if (mat == null)
                    {
                        continue; // legacy sub-path handled after sorting
                    }

                    float dist = Vector3.Distance(meshInfo.World.Translation, frame.CameraPosition);
                    var features = mat.GetFeatures(mesh);
                    var item = new RenderItem
                    {
                        Mesh                  = mesh,
                        SubMesh               = subMesh,
                        Material              = mat,
                        World                 = meshInfo.World,
                        WorldInverseTranspose = meshInfo.WorldInvertTranspose,
                        DistanceToCamera      = dist,
                        PropertyOverrides     = meshInfo.PropertyOverrides,
                        Features              = features,
                    };
                    item.SortKey = SortKeyGenerator.Generate(
                        mat.Queue,
                        mat.ShaderAssetId.GetHashCode(),
                        mat.Id.GetHashCode(),
                        vb.GetHashCode(),
                        dist);
                    _renderItems.Add(item);
                }
            }
            else if (meshInfo.Material != null)
            {
                var mat = meshInfo.Material;
                float dist = Vector3.Distance(meshInfo.World.Translation, frame.CameraPosition);
                var features = mat.GetFeatures(mesh);
                var item = new RenderItem
                {
                    Mesh                  = mesh,
                    SubMesh               = null,
                    Material              = mat,
                    World                 = meshInfo.World,
                    WorldInverseTranspose = meshInfo.WorldInvertTranspose,
                    DistanceToCamera      = dist,
                    PropertyOverrides     = meshInfo.PropertyOverrides,
                    Features              = features,
                };
                item.SortKey = SortKeyGenerator.Generate(
                    mat.Queue,
                    mat.ShaderAssetId.GetHashCode(),
                    mat.Id.GetHashCode(),
                    vb.GetHashCode(),
                    dist);
                _renderItems.Add(item);
            }
        }

        // Sort opaque front-to-back, transparent back-to-front — the SortKey encodes this.
        _renderItems.Sort(static (a, b) => a.SortKey.CompareTo(b.SortKey));

        // --- Phase 9: hardware-instanced draw for groups with ShaderFeature.Instanced ---
        // Items that go through instancing are removed from the regular draw list.
        if (_instanceBatcher is not null)
        {
            // Group by (VertexBuffer ptr, SortKey of first element) — same mesh + material
            var instanceGroups = new Dictionary<(IntPtr, ulong), List<RenderItem>>();
            var toRemove = new List<int>();

            for (int i = 0; i < _renderItems.Count; i++)
            {
                var item = _renderItems[i];
                if ((item.Features & ShaderFeature.Instanced) == 0)
                {
                    continue;
                }

                var groupKey = (item.Mesh.VertexBuffer!.Tag as IntPtr? ?? IntPtr.Zero, item.SortKey & ~0xFFFUL);
                if (!instanceGroups.TryGetValue(groupKey, out var list))
                {
                    list = new List<RenderItem>();
                    instanceGroups[groupKey] = list;
                }
                list.Add(item);
                toRemove.Add(i);
            }

            // Draw groups that exceed the threshold; put the rest back on the regular list
            foreach (var group in instanceGroups.Values)
            {
                if (group.Count < _instanceBatcher.MinInstanceThreshold)
                {
                    continue; // too small → handled by regular path
                }

                var firstItem = group[0];
                _stateCache.Apply(graphicsDevice, firstItem.Material, stats);
                var shader = (_variantLibrary is not null && firstItem.Material.ShaderAssetId != Guid.Empty)
                    ? _variantLibrary.Get(new ShaderVariantKey(firstItem.Material.ShaderAssetId, firstItem.Features))
                        ?? _legacyShaderWrapper!
                    : _legacyShaderWrapper!;
                _shaderCache.BindGlobals(shader, in context);
                _instanceBatcher.DrawInstancedGroup(group, shader, in context);
                stats.DrawCalls++;

                // Mark as drawn
                foreach (var item in group)
                    toRemove.Add(_renderItems.IndexOf(item));
            }

            // Remove instanced items from the regular list (largest index first to preserve indices)
            toRemove.Sort(static (a, b) => b.CompareTo(a));
            foreach (var idx in toRemove.Distinct())
                if (idx >= 0 && idx < _renderItems.Count)
                {
                    _renderItems.RemoveAt(idx);
                }
        }

        // --- Phase 10: delegate sorted items to ForwardRenderPipeline ---
        _pipeline.Render(context, _renderItems, _stateCache, _shaderCache, _legacyShaderWrapper!);

        // --- Legacy fallback: items with no material at all ---
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
            bool hasAnyMaterial = meshInfo.Material != null || mesh.SubMeshes.Any(s => s.Material != null);
            if (hasAnyMaterial)
            {
                continue;
            }

            graphicsDevice.SetVertexBuffer(vb);
            graphicsDevice.Indices = ib;

            int primitiveCount = ib.IndexCount / 3;
            DrawLegacy(graphicsDevice, mesh, meshInfo, frame, defaultTexture, 0, 0, primitiveCount);
        }

        _meshInfos.Clear();
    }

    private void DrawLegacy(GraphicsDevice graphicsDevice, StaticModelMesh mesh, MeshInfo meshInfo,
        in RenderFrame frame, Assets.Textures.Texture? defaultTexture,
        int baseVertex, int startIndex, int primitiveCount)
    {
        graphicsDevice.DepthStencilState = DepthStencilState.Default;
        graphicsDevice.RasterizerState   = RasterizerState.CullCounterClockwise;
        graphicsDevice.BlendState         = BlendState.Opaque;
        graphicsDevice.SamplerStates[0]   = SamplerState.AnisotropicClamp;

        // Bind lighting from DefaultLighting (Phase 5: replaces hardcoded values)
        var sw = _legacyShaderWrapper!;
        DefaultLighting.Bind(sw);

        var texture = mesh.Texture?.Resource ?? defaultTexture?.Resource;
        // Always select the matching technique so legacy items rendered after
        // a material call (which may have changed CurrentTechnique) are correct.
        _effect.CurrentTechnique = _effect.Techniques[
            texture != null ? "BasicEffect_PixelLighting_Texture" : "BasicEffect_PixelLighting"];
        _effect.Parameters["Texture"].SetValue(texture);
        _effect.Parameters["EyePosition"].SetValue(frame.CameraPosition);
        _effect.Parameters["World"].SetValue(meshInfo.World);
        _effect.Parameters["WorldInverseTranspose"].SetValue(meshInfo.WorldInvertTranspose);
        _effect.Parameters["WorldViewProj"].SetValue(meshInfo.World * frame.ViewProjection);

        foreach (EffectPass effectPass in _effect.CurrentTechnique.Passes)
        {
            effectPass.Apply();
            graphicsDevice.DrawIndexedPrimitives(mesh.PrimitiveType, baseVertex, startIndex, primitiveCount);
        }
    }

    private class MeshInfo
    {
        public StaticModelMesh? StaticModelMesh;
        public Matrix World;
        public Matrix WorldInvertTranspose;
        public MaterialBase? Material;
        /// <summary>Optional per-instance overrides (Phase 6).</summary>
        public MaterialPropertyBlock? PropertyOverrides;
    }
}
