using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DirLight = CasaEngine.Framework.Rendering.DirectionalLight;

namespace CasaEngine.Framework.Game.Components;

public class SkinnedMeshRendererComponent : DrawableGameComponent, IViewFlushableRenderer
{
    private readonly List<SkinnedMeshInfo> _meshInfos = new();
    private Effect _effect;
    private ShaderWrapper _shader;
    private CasaEngineGame _game;

    /// <summary>
    /// Default scene lighting for skinned meshes. Same values as StaticMeshRendererComponent.
    /// </summary>
    public LightingContext DefaultLighting { get; } = new LightingContext
    {
        ActiveDirectionalLightCount = 3,
        AmbientColor = new Vector3(0.05f, 0.05f, 0.05f),
    };

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
        _shader = new ShaderWrapper(_effect);

        // Provide a 1×1 white fallback texture for skinned meshes without textures.
        if (RiggedModelLoader.DefaultTexture == null)
        {
            var white = new Texture2D(Game.GraphicsDevice, 1, 1);
            white.SetData(new[] { Color.White });
            RiggedModelLoader.DefaultTexture = white;
        }

        // Initialise lighting to match StaticMeshRendererComponent defaults
        DefaultLighting.DirectionalLights[0] = new DirLight(
            new Vector3(-0.5265408f, -0.5735765f, -0.6275069f),
            new Vector3(1f, 0.9607844f, 0.8078432f),
            new Vector3(1f, 0.9607844f, 0.8078432f));
        DefaultLighting.DirectionalLights[1] = new DirLight(
            new Vector3(0.7198464f, 0.3420201f, 0.6040227f),
            new Vector3(0.9647059f, 0.7607844f, 0.4078432f),
            Vector3.Zero);
        DefaultLighting.DirectionalLights[2] = new DirLight(
            new Vector3(0.4545195f, -0.7660444f, 0.4545195f),
            new Vector3(0.3231373f, 0.3607844f, 0.3937255f),
            new Vector3(0.3231373f, 0.3607844f, 0.3937255f));

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
        GraphicsDevice.BlendState = BlendState.NonPremultiplied;
        GraphicsDevice.SetVertexBuffer(null);
        GraphicsDevice.Indices = null;

        _effect.CurrentTechnique = _effect.Techniques["RiggedModelDraw"];

        _shader.SetParameter(ShaderParameterNames.EyePosition, frame.CameraPosition);

        // Material defaults for skinned meshes
        _shader.SetParameter(ShaderParameterNames.DiffuseColor, new Vector4(1f, 1f, 1f, 1f));
        _shader.SetParameter(ShaderParameterNames.EmissiveColor, Vector3.Zero);
        _shader.SetParameter(ShaderParameterNames.SpecularColor, new Vector3(0.3f, 0.3f, 0.3f));
        _shader.SetParameter(ShaderParameterNames.SpecularPower, 16.0f);

        // Use the engine's shared lighting (same 3 directional lights as static meshes)
        DefaultLighting.Bind(_shader);

        foreach (var meshInfo in _meshInfos)
        {
            meshInfo.SkinnedMesh.Effect = _effect;
            meshInfo.SkinnedMesh.Draw(GraphicsDevice, meshInfo.World, frame.ViewProjection);
        }

        _meshInfos.Clear();
    }

    private class SkinnedMeshInfo
    {
        public RiggedModel? SkinnedMesh;
        public Matrix World { get; set; }
    }
}
