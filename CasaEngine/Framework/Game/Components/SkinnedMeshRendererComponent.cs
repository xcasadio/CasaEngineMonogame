using CasaEngine.Engine.Animations;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game.Components;

public class SkinnedMeshRendererComponent : DrawableGameComponent, IViewFlushableRenderer
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

    public void AddMesh(RiggedModel mesh, Matrix world)
    {
        _meshInfos.Add(new SkinnedMeshInfo
        {
            SkinnedMesh = mesh,
            World = world,
        });
    }

    protected override void LoadContent()
    {
        _effect = Game.Content.Load<Effect>("Shaders\\skinEffect");
        base.LoadContent();
    }

    /// <inheritdoc/>
    public void Flush(in RenderFrame frame)
    {
        if (_meshInfos.Count == 0)
        {
            return;
        }

        GraphicsDevice graphicsDevice = _effect.GraphicsDevice;
        graphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        //GraphicsDevice.RasterizerState = new RasterizerState() { CullMode = CullMode.CullClockwiseFace, FillMode = FillMode.WireFrame };
        GraphicsDevice.BlendState = BlendState.NonPremultiplied;
        //GraphicsDevice.SamplerStates[0] = SamplerState.AnisotropicClamp;
        GraphicsDevice.SetVertexBuffer(null);
        GraphicsDevice.Indices = null;

        _effect.CurrentTechnique = _effect.Techniques["RiggedModelDraw"];

        _effect.Parameters["View"].SetValue(frame.View);
        _effect.Parameters["Projection"].SetValue(frame.Projection);
        _effect.Parameters["CameraPosition"].SetValue(frame.CameraPosition);

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
            meshInfo.SkinnedMesh.Effect = _effect;
            meshInfo.SkinnedMesh.Draw(GraphicsDevice, meshInfo.World);
        }

        _meshInfos.Clear();
    }

    private class SkinnedMeshInfo
    {
        public RiggedModel? SkinnedMesh;
        public Matrix World { get; set; }
    }
}
