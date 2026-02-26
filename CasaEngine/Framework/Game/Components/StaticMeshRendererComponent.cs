using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game.Components;

public class StaticMeshRendererComponent : DrawableGameComponent, IViewFlushableRenderer
{
    private readonly List<MeshInfo> _meshInfos = new();
    private Effect _effect;
    private ShaderWrapper? _legacyShaderWrapper;

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

        var defaultTexture = (Game as CasaEngineGame)?.AssetContentManager.GetAsset<Assets.Textures.Texture>(Assets.Textures.Texture.DefaultTextureName);

        // Build a lightweight RenderContext (no lighting yet — Phase 5 will fill it in)
        var context = new RenderContext
        {
            Device  = graphicsDevice,
            Frame   = frame,
        };

        foreach (var meshInfo in _meshInfos)
        {
            if (meshInfo.StaticModelMesh == null) continue;

            var vb = meshInfo.StaticModelMesh.VertexBuffer;
            var ib = meshInfo.StaticModelMesh.IndexBuffer;
            if (vb == null || ib == null) continue;

            graphicsDevice.SetVertexBuffer(vb);
            graphicsDevice.Indices = ib;

            var mesh = meshInfo.StaticModelMesh;

            if (mesh.SubMeshes.Count > 0)
            {
                // --- Multi-material path: one draw call per SubMesh ---
                foreach (var subMesh in mesh.SubMeshes)
                {
                    var mat = subMesh.Material ?? meshInfo.Material;
                    if (mat != null)
                    {
                        ApplyRenderStates(graphicsDevice, mat);
                        var shader = _legacyShaderWrapper!;
                        mat.Bind(shader, in context, meshInfo.World);

                        for (int p = 0; p < shader.PassCount; p++)
                        {
                            shader.ApplyPass(p);
                            graphicsDevice.DrawIndexedPrimitives(mesh.PrimitiveType,
                                subMesh.VertexOffset, subMesh.IndexStart, subMesh.PrimitiveCount);
                        }
                    }
                    else
                    {
                        // Legacy sub-path
                        DrawLegacy(graphicsDevice, mesh, meshInfo, frame, defaultTexture,
                            subMesh.VertexOffset, subMesh.IndexStart, subMesh.PrimitiveCount);
                    }
                }
            }
            else if (meshInfo.Material != null)
            {
                // --- Single-material path ---
                ApplyRenderStates(graphicsDevice, meshInfo.Material);

                var shader = _legacyShaderWrapper!;
                meshInfo.Material.Bind(shader, in context, meshInfo.World);

                for (int p = 0; p < shader.PassCount; p++)
                {
                    shader.ApplyPass(p);
                    int primitiveCount = ib.IndexCount / 3;
                    graphicsDevice.DrawIndexedPrimitives(mesh.PrimitiveType, 0, 0, primitiveCount);
                }
            }
            else
            {
                // --- Legacy path: hardcoded basicEffect (backwards compatibility) ---
                int primitiveCount = ib.IndexCount / 3;
                DrawLegacy(graphicsDevice, mesh, meshInfo, frame, defaultTexture, 0, 0, primitiveCount);
            }
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

    private static void ApplyRenderStates(GraphicsDevice device, MaterialBase material)
    {
        device.BlendState         = material.BlendState         ?? BlendState.Opaque;
        device.DepthStencilState  = material.DepthStencilState  ?? DepthStencilState.Default;
        device.RasterizerState    = material.RasterizerState    ?? RasterizerState.CullCounterClockwise;
        device.SamplerStates[0]   = material.SamplerState       ?? SamplerState.AnisotropicClamp;
    }

    private class MeshInfo
    {
        public StaticModelMesh? StaticModelMesh;
        public Matrix World;
        public Matrix WorldInvertTranspose;
        public MaterialBase? Material;
    }
}
