using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using CasaEngine.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace SandBoxGame
{
    public class SandBoxGame : CasaEngineGame
    {
        private Entity _boxEntity;
        private StaticMesh _staticMesh;
        private Effect _effect;
        private Material _material;

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
            GameManager.DefaultSpriteFont = Content.Load<SpriteFont>("GizmoFont");

            var world = new World();
            GameManager.CurrentWorld = world;

            //============ Camera ===============
            var entity = new Entity();
            var camera = new ArcBallCameraComponent(entity);
            camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
            //var camera = new Camera3dIn2dAxisComponent(entity);
            //camera.Target = new Vector3(Window.ClientBounds.Size.X / 2f, Window.ClientBounds.Size.Y / 2f, 0.0f);
            entity.ComponentManager.Components.Add(camera);
            var gamePlayComponent = new GamePlayComponent(entity);
            entity.ComponentManager.Components.Add(gamePlayComponent);
            gamePlayComponent.ExternalComponent = new ScriptArcBallCamera();
            world.AddEntityImmediately(entity);

            //============ Box ===============
            entity = new Entity();
            _boxEntity = entity;
            entity.Coordinates.LocalPosition = Vector3.Up * 0.5f;
            var meshComponent = new StaticMeshComponent(entity);
            entity.ComponentManager.Components.Add(meshComponent);
            meshComponent.Mesh = new BoxPrimitive(GraphicsDevice).CreateMesh();
            meshComponent.Mesh.Initialize(GraphicsDevice, GameManager.AssetContentManager);
            meshComponent.Mesh.Texture = new Texture(Texture2D.FromFile(GraphicsDevice, @"Content\checkboard.png"));
            _staticMesh = meshComponent.Mesh;
            world.AddEntityImmediately(entity);

            //============ Effect ===============
            _material = new Material();
            var shaderCompiled = ShaderCompiler.Compile("", null, TargetPlatform.Windows);
            _effect = new Effect(GraphicsDevice, shaderCompiled.ByteCode);

            base.LoadContent();

            GameManager.ActiveCamera = camera;
        }

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
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.SamplerStates[0] = SamplerState.AnisotropicClamp;

            GraphicsDevice.SetVertexBuffer(_staticMesh.VertexBuffer);
            GraphicsDevice.Indices = _staticMesh.IndexBuffer;

            _effect.Parameters["Texture"].SetValue(_staticMesh.Texture?.Resource);
            //_effect.Parameters["EyePosition"].SetValue(_boxEntity.CameraPosition);
            _effect.Parameters["World"].SetValue(_boxEntity.Coordinates.WorldMatrix);
            //_effect.Parameters["WorldInverseTranspose"].SetValue((_boxEntity.Coordinates.WorldMatrix.Invert().Transpose());
            _effect.Parameters["WorldViewProj"].SetValue(
                _boxEntity.Coordinates.WorldMatrix * GameManager.ActiveCamera.ViewMatrix * GameManager.ActiveCamera.ProjectionMatrix);

            foreach (EffectPass effectPass in _effect.CurrentTechnique.Passes)
            {
                effectPass.Apply();
                int primitiveCount = _staticMesh.IndexBuffer.IndexCount / 3;
                GraphicsDevice.DrawIndexedPrimitives(_staticMesh.PrimitiveType, 0, 0, primitiveCount);
            }

            base.Draw(gameTime);
        }
    }
}