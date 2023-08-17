using CasaEngine.Engine.Animations;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game.Components;

public class SkinnedMeshRendererComponent : DrawableGameComponent
{
    private readonly List<SkinnedMeshInfo> _meshInfos = new();
    private Effect _effect;
    private CasaEngineGame _game;

    public SkinnedMeshRendererComponent(CasaEngineGame game) : base(game)
    {
        _game = game;
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.MeshComponent;
        DrawOrder = (int)ComponentDrawOrder.MeshComponent;
    }

    public void AddMesh(RiggedModel mesh, Matrix world, Matrix view, Matrix projection, Vector3 cameraPosition)
    {
        _meshInfos.Add(new SkinnedMeshInfo
        {
            SkinnedMesh = mesh,
            World = world,
            View = view,
            ViewProjection = view * projection,
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
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        //GraphicsDevice.RasterizerState = new RasterizerState() { CullMode = CullMode.CullClockwiseFace, FillMode = FillMode.WireFrame };
        GraphicsDevice.BlendState = BlendState.NonPremultiplied;
        //GraphicsDevice.SamplerStates[0] = SamplerState.AnisotropicClamp;
        GraphicsDevice.SetVertexBuffer(null);
        GraphicsDevice.Indices = null;

        var camera = _game.GameManager.ActiveCamera;
        if (camera == null)
        {
            return;
        }

        _effect.CurrentTechnique = _effect.Techniques["RiggedModelDraw"];

        _effect.Parameters["View"].SetValue(camera.ViewMatrix);
        _effect.Parameters["Projection"].SetValue(camera.ProjectionMatrix);
        _effect.Parameters["CameraPosition"].SetValue(camera.Position);

        // set up the effect initially to change how you want the shader to behave
        _effect.Parameters["AmbientAmt"].SetValue(.15f);
        _effect.Parameters["DiffuseAmt"].SetValue(.6f);
        _effect.Parameters["SpecularAmt"].SetValue(.25f);
        _effect.Parameters["SpecularSharpness"].SetValue(.88f);
        _effect.Parameters["SpecularLightVsTexelInfluence"].SetValue(.40f);

        _effect.Parameters["WorldLightPosition"].SetValue(new Vector3(0f, 0f, 1200f));
        _effect.Parameters["LightColor"].SetValue(new Vector4(.099f, .099f, .999f, 1.0f));

        foreach (var meshInfo in _meshInfos)
        {
            meshInfo.SkinnedMesh.effect = _effect;
            meshInfo.SkinnedMesh.Draw(GraphicsDevice, meshInfo.World);
        }

        _meshInfos.Clear();
    }

    private class SkinnedMeshInfo
    {
        public RiggedModel? SkinnedMesh;
        public Matrix World { get; set; }
        public Matrix View { get; set; }
        public Matrix ViewProjection { get; set; }
        public Vector3 CameraPosition { get; set; }
    }
}
