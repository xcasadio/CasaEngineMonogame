using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Draw;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

    public StaticMeshRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.MeshComponent;
        DrawOrder = (int)ComponentDrawOrder.MeshComponent;
    }

    /// <summary>Enqueue a <see cref="StaticModelMesh"/> sub-mesh for rendering.</summary>
    public void AddMesh(StaticModelMesh staticModelMesh, Matrix world, Matrix worldInvertTranspose)
    {
        _meshInfos.Add(new MeshInfo
        {
            StaticModelMesh = staticModelMesh,
            World = world,
            WorldInvertTranspose = worldInvertTranspose,
            Material = staticModelMesh.Material,
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
        
        _effect.Parameters["DirLight0Direction"].SetValue(new Vector3(-0.5265408f, -0.5735765f, -0.6275069f));
        _effect.Parameters["DirLight0DiffuseColor"].SetValue(new Vector3(1, 0.9607844f, 0.8078432f));
        _effect.Parameters["DirLight0SpecularColor"].SetValue(new Vector3(1, 0.9607844f, 0.8078432f));
        
        _effect.Parameters["DirLight1Direction"].SetValue(new Vector3(0.7198464f, 0.3420201f, 0.6040227f));
        _effect.Parameters["DirLight1DiffuseColor"].SetValue(new Vector3(0.9647059f, 0.7607844f, 0.4078432f));
        _effect.Parameters["DirLight1SpecularColor"].SetValue(Vector3.Zero);
        
        _effect.Parameters["DirLight2Direction"].SetValue(new Vector3(0.4545195f, -0.7660444f, 0.4545195f));
        _effect.Parameters["DirLight2DiffuseColor"].SetValue(new Vector3(0.3231373f, 0.3607844f, 0.3937255f));
        _effect.Parameters["DirLight2SpecularColor"].SetValue(new Vector3(0.3231373f, 0.3607844f, 0.3937255f));

        _legacyShaderWrapper = new ShaderWrapper(_effect);

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
            Device  = graphicsDevice,
            Frame   = frame,
            Stats   = stats,
        };

        // --- Phase 4: build a sorted RenderItem list ---
        _renderItems.Clear();

        foreach (var meshInfo in _meshInfos)
        {
            if (meshInfo.StaticModelMesh == null) continue;
            var vb = meshInfo.StaticModelMesh.VertexBuffer;
            var ib = meshInfo.StaticModelMesh.IndexBuffer;
            if (vb == null || ib == null) continue;

            var mesh = meshInfo.StaticModelMesh;

            if (mesh.SubMeshes.Count > 0)
            {
                foreach (var subMesh in mesh.SubMeshes)
                {
                    var mat = subMesh.Material ?? meshInfo.Material;
                    if (mat == null) continue; // legacy sub-path handled after sorting

                    float dist = Vector3.Distance(meshInfo.World.Translation, frame.CameraPosition);
                    var item = new RenderItem
                    {
                        Mesh                  = mesh,
                        SubMesh               = subMesh,
                        Material              = mat,
                        World                 = meshInfo.World,
                        WorldInverseTranspose = meshInfo.WorldInvertTranspose,
                        DistanceToCamera      = dist,
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
                var item = new RenderItem
                {
                    Mesh                  = mesh,
                    SubMesh               = null,
                    Material              = mat,
                    World                 = meshInfo.World,
                    WorldInverseTranspose = meshInfo.WorldInvertTranspose,
                    DistanceToCamera      = dist,
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

        // --- Draw sorted material items ---
        foreach (var item in _renderItems)
        {
            var vb = item.Mesh.VertexBuffer!;
            var ib = item.Mesh.IndexBuffer!;
            graphicsDevice.SetVertexBuffer(vb);
            graphicsDevice.Indices = ib;

            _stateCache.Apply(graphicsDevice, item.Material, stats);

            var shader = _legacyShaderWrapper!;
            _shaderCache.BindGlobals(shader, in context);
            item.Material.Bind(shader, in context, item.World);

            if (item.SubMesh is { } sub)
            {
                for (int p = 0; p < shader.PassCount; p++)
                {
                    shader.ApplyPass(p);
                    graphicsDevice.DrawIndexedPrimitives(item.Mesh.PrimitiveType,
                        sub.VertexOffset, sub.IndexStart, sub.PrimitiveCount);
                }
            }
            else
            {
                int primitiveCount = ib.IndexCount / 3;
                for (int p = 0; p < shader.PassCount; p++)
                {
                    shader.ApplyPass(p);
                    graphicsDevice.DrawIndexedPrimitives(item.Mesh.PrimitiveType, 0, 0, primitiveCount);
                }
            }
            stats.DrawCalls++;
        }

        // --- Legacy fallback: items with no material at all ---
        foreach (var meshInfo in _meshInfos)
        {
            if (meshInfo.StaticModelMesh == null) continue;
            var vb = meshInfo.StaticModelMesh.VertexBuffer;
            var ib = meshInfo.StaticModelMesh.IndexBuffer;
            if (vb == null || ib == null) continue;

            var mesh = meshInfo.StaticModelMesh;
            bool hasAnyMaterial = meshInfo.Material != null || mesh.SubMeshes.Any(s => s.Material != null);
            if (hasAnyMaterial) continue;

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

        var texture = mesh.Texture?.Resource ?? defaultTexture?.Resource;
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
    }
}
