using CasaEngine.Core.Helpers;
using CasaEngine.Engine.Animations;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
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

    public void AddMesh(StaticMesh staticMesh, Material material, Matrix world, Matrix worldViewProj, Vector3 cameraPosition)
    {
        _meshInfos.Add(new MeshInfo
        {
            StaticMesh = staticMesh,
            Material = material,
            World = world,
            WorldViewProj = worldViewProj,
            CameraPosition = cameraPosition
        });
    }

    protected override void LoadContent()
    {
        _effect = Game.Content.Load<Effect>("Shaders\\basicEffect");
        _effect.CurrentTechnique = _effect.Techniques["BasicEffect_PixelLighting_Texture"];

        _effect.Parameters["DiffuseColor"].SetValue(Vector4.One);
        _effect.Parameters["EmissiveColor"].SetValue(Vector3.Zero);
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

        //float alpha = 1.0f;
        //Vector4 diffuse = new Vector4();
        //Vector3 emissive = new Vector3();
        //Vector3 ambientLightColor = new Vector3(0.75f, 0.75f, 0.75f); //new Vector3(0.05333332f, 0.09882354f, 0.1819608f);        
        //
        //
        //diffuse.X = diffuseColor.X * alpha;
        //diffuse.Y = diffuseColor.Y * alpha;
        //diffuse.Z = diffuseColor.Z * alpha;
        //diffuse.W = alpha;
        //
        //emissive.X = (emissiveColor.X + ambientLightColor.X * diffuseColor.X) * alpha;
        //emissive.Y = (emissiveColor.Y + ambientLightColor.Y * diffuseColor.Y) * alpha;
        //emissive.Z = (emissiveColor.Z + ambientLightColor.Z * diffuseColor.Z) * alpha;
        //
        //diffuseColorParam.SetValue(diffuse);
        //emissiveColorParam.SetValue(emissive);

        base.LoadContent();
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice graphicsDevice = _effect.GraphicsDevice;
        graphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        GraphicsDevice.BlendState = BlendState.AlphaBlend;
        GraphicsDevice.SamplerStates[0] = SamplerState.AnisotropicClamp;

        foreach (var meshInfo in _meshInfos)
        {
            graphicsDevice.SetVertexBuffer(meshInfo.StaticMesh.VertexBuffer);
            graphicsDevice.Indices = meshInfo.StaticMesh.IndexBuffer;

            _effect.Parameters["Texture"].SetValue(meshInfo.StaticMesh.Texture?.Resource);
            _effect.Parameters["EyePosition"].SetValue(meshInfo.CameraPosition);
            _effect.Parameters["World"].SetValue(meshInfo.World);
            _effect.Parameters["WorldInverseTranspose"].SetValue(meshInfo.World.Invert().Transpose());
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
        public Material? Material;
        public Vector3 CameraPosition;
        public Matrix World;
        public Matrix WorldViewProj;
    }
}
