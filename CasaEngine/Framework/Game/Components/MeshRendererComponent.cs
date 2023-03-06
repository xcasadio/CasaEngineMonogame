using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game.Components;

public class MeshRendererComponent : DrawableGameComponent
{
    private List<MeshInfo> _meshInfos = new();
    private Effect _effect;

    public MeshRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.MeshComponent;
        DrawOrder = (int)ComponentDrawOrder.MeshComponent;
    }

    public void AddMesh(Mesh mesh, Matrix worldViewProj)
    {
        _meshInfos.Add(new MeshInfo { Mesh = mesh, WorldViewProj = worldViewProj });
    }

    protected override void LoadContent()
    {
        _effect = Game.Content.Load<Effect>("simple");

        base.LoadContent();
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice graphicsDevice = _effect.GraphicsDevice;
        graphicsDevice.DepthStencilState = DepthStencilState.Default;

        foreach (var meshInfo in _meshInfos)
        {
            graphicsDevice.SetVertexBuffer(meshInfo.Mesh.VertexBuffer);
            graphicsDevice.Indices = meshInfo.Mesh.IndexBuffer;

            _effect.Parameters["Texture"].SetValue(meshInfo.Mesh.Texture);
            _effect.Parameters["WorldViewProj"].SetValue(meshInfo.WorldViewProj);

            foreach (EffectPass effectPass in _effect.CurrentTechnique.Passes)
            {
                effectPass.Apply();
                int primitiveCount = meshInfo.Mesh.IndexBuffer.IndexCount / 3;
                graphicsDevice.DrawIndexedPrimitives(meshInfo.Mesh.PrimitiveType, 0, 0, primitiveCount);
            }
        }

        _meshInfos.Clear();
    }

    private class MeshInfo
    {
        public Mesh Mesh;
        public Matrix WorldViewProj;
    }
}
