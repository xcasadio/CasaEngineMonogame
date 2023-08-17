using CasaEngine.Engine.Animations;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game.Components;

public class SkinnedMeshRendererComponent : DrawableGameComponent
{
    private readonly List<SkinnedMeshInfo> _meshInfos = new();
    private Effect _effect;

    public SkinnedMeshRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.MeshComponent;
        DrawOrder = (int)ComponentDrawOrder.MeshComponent;
    }

    public void AddMesh(SkinModel mesh, Matrix world, Matrix view, Matrix projection, Vector3 cameraPosition)
    {
        _meshInfos.Add(new SkinnedMeshInfo
        {
            SkinnedMesh = mesh,
            World = world,
            View = view,
            Projection = projection,
            CameraPosition = cameraPosition
        });
    }

    protected override void LoadContent()
    {
        _effect = Game.Content.Load<Effect>("Shaders\\skinEffect");
        base.LoadContent();
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice graphicsDevice = _effect.GraphicsDevice;
        graphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        //GraphicsDevice.RasterizerState = new RasterizerState() { CullMode = CullMode.CullClockwiseFace, FillMode = FillMode.WireFrame };
        GraphicsDevice.BlendState = BlendState.NonPremultiplied;
        //GraphicsDevice.SamplerStates[0] = SamplerState.AnisotropicClamp;
        //GraphicsDevice.SetVertexBuffer(null);
        //GraphicsDevice.Indices = null;

        foreach (var meshInfo in _meshInfos)
        {
            //meshInfo.SkinnedMesh.DrawMesh(0, meshInfo.View * meshInfo.Projection, meshInfo.View, meshInfo.CameraPosition, meshInfo.World);
            meshInfo.SkinnedMesh.Draw(meshInfo.View * meshInfo.Projection, meshInfo.View, meshInfo.CameraPosition, meshInfo.World);
        }

        _meshInfos.Clear();
    }

    private class SkinnedMeshInfo
    {
        public SkinModel? SkinnedMesh;
        public Matrix World { get; set; }
        public Matrix View { get; set; }
        public Matrix Projection { get; set; }
        public Vector3 CameraPosition { get; set; }
    }
}
