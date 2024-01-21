using System.IO;
using System.Linq;
using CasaEngine.Core.Helpers;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Materials.Graph;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;
using Vector3 = Microsoft.Xna.Framework.Vector3;

#if EDITOR
using CasaEngine.Shaders;
#endif

namespace SandBoxGame
{
    public class SandBoxGame : CasaEngineGame
    {
        private AActor _boxEntity;
        private StaticMesh _staticMesh;

        private Effect _effectColor;
        private Material _materialColor;

        private Effect _effectTexture;
        private Effect _effectTexture2;
        private Material _materialTexture;

        protected override void Initialize()
        {
            new GridComponent(this);
            new AxisComponent(this);
            base.Initialize();

            //IsMouseVisible = true;

            //GameManager.SpriteRendererComponent.IsDrawCollisionsEnabled = true;
            //GameManager.SpriteRendererComponent.IsDrawSpriteOriginEnabled = true;

            //PhysicsDebugViewRendererComponent.DisplayPhysics = false;
        }

        protected override void LoadContent()
        {
            var world = new World();
            world.Initialize(this);
            GameManager.CurrentWorld = world;

            //============ Camera ===============
            var entity = new AActor { Name = "camera" };
            var camera = new ArcBallCameraComponent();
            //var camera = new Camera3dIn2dAxisComponent(entity);
            //camera.Target = new Vector3(Window.ClientBounds.Size.X / 2f, Window.ClientBounds.Size.Y / 2f, 0.0f);
            entity.AddComponent(camera);
            entity.GameplayProxy = new ScriptArcBallCamera();
            world.AddEntity(entity);

            //============ Box ===============
            _boxEntity = new AActor { Name = "box" };
            var meshComponent = new StaticMeshComponent();
            _boxEntity.RootComponent.Position = Vector3.Up * 0.5f;
            _boxEntity.AddComponent(meshComponent);
            meshComponent.Mesh = new BoxPrimitive(GraphicsDevice).CreateMesh();
            meshComponent.Mesh.Initialize(GraphicsDevice, GameManager.AssetContentManager);
            meshComponent.Mesh.Texture = new Texture(Texture2D.FromFile(GraphicsDevice, @"Content\checkboard.png"));
            _staticMesh = meshComponent.Mesh;

            world.AddEntity(_boxEntity);

            //============ Effect ===============
            _materialColor = new Material();
            var materialDiffuse = new MaterialColor();
            materialDiffuse.Color = Color.Green;
            _materialColor.Diffuse = materialDiffuse;
            /*
            var byteCode = CompileShaderFromMaterial(_materialColor);
            _effectColor = new Effect(GraphicsDevice, byteCode);
            
            //=====
            _materialTexture = new Material();
            var materialTextureDiffuse = new MaterialTexture();
            materialTextureDiffuse.Texture = new Texture(Texture2D.FromFile(GraphicsDevice, @"Content\checkboard.png"));
            _materialTexture.Diffuse = materialTextureDiffuse;

            byteCode = CompileShaderFromMaterial(_materialTexture);
            _effectTexture = new Effect(GraphicsDevice, byteCode);

            //===== Test
            var materialGraph = new MaterialGraph();
            materialGraph.DiffuseSlot = new MaterialGraphNodeSlot();
            //Texture -> Diffuse
            var materialGraphNodeTexture = new MaterialGraphNodeTexture();
            materialGraphNodeTexture._inputSlot = new MaterialGraphNodeSlot();
            materialGraphNodeTexture.Texture = new Texture(Texture2D.FromFile(GraphicsDevice, @"Content\checkboard.png"));
            materialGraph.DiffuseSlot.Node = materialGraphNodeTexture;
            //DisplacementTexture -> Texture -> Diffuse
            var materialGraphNodeDisplacementTexture = new MaterialGraphNodeDisplacementTexture();
            materialGraphNodeDisplacementTexture._inputSlot = new MaterialGraphNodeSlot();
            materialGraphNodeDisplacementTexture.speed = 0.1f;
            materialGraphNodeTexture._inputSlot.Node = materialGraphNodeDisplacementTexture;
            //UV -> DisplacementTexture -> Texture -> Diffuse
            materialGraphNodeDisplacementTexture._inputSlot.Node = new MaterialGraphNodeTextureUV();

            byteCode = CompileShaderFromMaterialGraph(materialGraph);
            _effectTexture2 = new Effect(GraphicsDevice, byteCode);
            */
            base.LoadContent();

            camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
            GameManager.ActiveCamera = camera;
        }
        /*
        private byte[] CompileShaderFromMaterial(Material material)
        {
            var shaderWriter = new ShaderWriter();
            var shaderCode = shaderWriter.Compile(material);
            var tempFileName = Path.GetTempFileName();
            File.WriteAllText(tempFileName, shaderCode);
            var shaderCompiled = ShaderCompiler.Compile(tempFileName, null, TargetPlatform.Windows);
            return shaderCompiled.ByteCode;
        }

        private byte[] CompileShaderFromMaterialGraph(MaterialGraph materialGraph)
        {
            var shaderWriter = new ShaderWriter2();
            var shaderCode = shaderWriter.Compile(materialGraph);
            var tempFileName = Path.GetTempFileName();
            File.WriteAllText(tempFileName, shaderCode);
            var shaderCompiled = ShaderCompiler.Compile(tempFileName, null, TargetPlatform.Windows);
            return shaderCompiled.ByteCode;
        }
        */
        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                Exit();
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.SamplerStates[0] = SamplerState.AnisotropicClamp;

            var samplerState = new SamplerState();
            samplerState.AddressU = TextureAddressMode.Wrap;
            samplerState.AddressV = TextureAddressMode.Wrap;
            samplerState.Filter = TextureFilter.Point;
            samplerState.FilterMode = TextureFilterMode.Default;
            GraphicsDevice.SamplerStates[0] = samplerState;

            GraphicsDevice.SetVertexBuffer(_staticMesh.VertexBuffer);
            GraphicsDevice.Indices = _staticMesh.IndexBuffer;

            DrawBoxWithEffect(gameTime, _effectColor, _materialColor, _boxEntity.RootComponent.WorldMatrix * Matrix.CreateTranslation(Vector3.UnitX * 2f));
            DrawBoxWithEffect(gameTime, _effectTexture, _materialTexture, _boxEntity.RootComponent.WorldMatrix * Matrix.CreateTranslation(-Vector3.UnitX * 2f));
            DrawBoxWithEffect(gameTime, _effectTexture2, _materialTexture, _boxEntity.RootComponent.WorldMatrix * Matrix.CreateTranslation(-Vector3.UnitX * 4f));
        }

        private void DrawBoxWithEffect(GameTime gameTime, Effect effect, Material material, Matrix world)
        {
            if (material.Diffuse is MaterialTexture materialTexture)
            {
                effect.Parameters["Texture"].SetValue(materialTexture.Texture?.Resource);
            }

            EffectParameter param = effect.Parameters.FirstOrDefault(x => x.Name == "TotalElapsedTime");
            if (param != null)
            {
                var totalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
                param.SetValue(totalSeconds);
            }

            // pour le deplacement de texture : uv + GameTime
            // pour la repetition de texture : uv * repetition
            //if (material.Diffuse is MaterialTexture materialTexture) 
            //{
            //    effect.Parameters["GameTime"].SetValue(GameTimeHelper.ConvertTotalTimeToSeconds(gameTime));
            //}

            //_effect.Parameters["Texture"].SetValue(_staticMesh.Texture?.Resource);
            //_effect.Parameters["EyePosition"].SetValue(_boxEntity.CameraPosition);
            //_effect.Parameters["World"].SetValue(_boxEntity.Coordinates.WorldMatrix);
            //_effect.Parameters["WorldInverseTranspose"].SetValue((_boxEntity.Coordinates.WorldMatrix.Invert().Transpose());

            effect.Parameters["WorldViewProj"].SetValue(world * GameManager.ActiveCamera.ViewMatrix * GameManager.ActiveCamera.ProjectionMatrix);

            foreach (EffectPass effectPass in effect.CurrentTechnique.Passes)
            {
                effectPass.Apply();
                int primitiveCount = _staticMesh.IndexBuffer.IndexCount / 3;
                GraphicsDevice.DrawIndexedPrimitives(_staticMesh.PrimitiveType, 0, 0, primitiveCount);
            }
        }
    }
}