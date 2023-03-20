using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game.Components;

public class StaticMeshRendererComponent : DrawableGameComponent
{
    private readonly List<MeshInfo> _meshInfos = new();
    private Effect _effect;

    public StaticMeshRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.MeshComponent;
        DrawOrder = (int)ComponentDrawOrder.MeshComponent;
    }

    public void AddMesh(StaticMesh staticMesh, Matrix worldViewProj)
    {
        _meshInfos.Add(new MeshInfo { StaticMesh = staticMesh, WorldViewProj = worldViewProj });
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
            graphicsDevice.SetVertexBuffer(meshInfo.StaticMesh.VertexBuffer);
            graphicsDevice.Indices = meshInfo.StaticMesh.IndexBuffer;

            _effect.Parameters["Texture"].SetValue(meshInfo.StaticMesh.Texture);
            _effect.Parameters["WorldViewProj"].SetValue(meshInfo.WorldViewProj);

            foreach (EffectPass effectPass in _effect.CurrentTechnique.Passes)
            {
                effectPass.Apply();
                int primitiveCount = meshInfo.StaticMesh.IndexBuffer.IndexCount / 3;
                graphicsDevice.DrawIndexedPrimitives(meshInfo.StaticMesh.PrimitiveType, 0, 0, primitiveCount);
            }
        }

        _meshInfos.Clear();
    }

    private class MeshInfo
    {
        public StaticMesh? StaticMesh;
        public Matrix WorldViewProj;
    }
}
