using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace DemosGame
{
    public class DemosGame : CasaEngineGame
    {
        protected override void Initialize()
        {
            base.Initialize();
            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {
            var world = new World();
            GameManager.CurrentWorld = world;

            //============ Camera ===============
            var entity = new Entity();
            var camera = new ArcBallCameraComponent(entity);
            entity.ComponentManager.Components.Add(camera);
            GameManager.ActiveCamera = camera;
            camera.SetCamera(Vector3.Backward * 15 + Vector3.Up * 12, Vector3.Zero, Vector3.Up);
            world.AddEntityImmediately(entity);

            //============ Create ground ===============
            entity = new Entity();
            var physicsComponent = new PhysicsComponent(entity);
            entity.ComponentManager.Components.Add(physicsComponent);
            physicsComponent.PhysicsType = PhysicsType.Static;
            physicsComponent.Shape = new Box { Size = new Vector3(50, 1, 50) };
            physicsComponent.Mass = 0.0f;
            var meshComponent = new StaticMeshComponent(entity);
            entity.ComponentManager.Components.Add(meshComponent);
            meshComponent.Mesh = new BoxPrimitive(GraphicsDevice, 50, 1, 50).CreateMesh();
            meshComponent.Mesh.Initialize(GraphicsDevice);
            meshComponent.Mesh.Texture = new Texture(GraphicsDevice, @"Content\checkboard.png", GameManager.AssetContentManager);
            world.AddEntityImmediately(entity);

            //============ Create box ===============
            const float mass = 1.0f;
            const int ArraySizeX = 5, ArraySizeY = 5, ArraySizeZ = 5;
            Vector3 start = new Vector3(
                    -(float)ArraySizeX + (float)ArraySizeX / 2f,
                    ArraySizeY + 10,
                    -(float)ArraySizeZ + (float)ArraySizeZ / 2f);

            var boxPrimitive = new BoxPrimitive(GraphicsDevice, 1, 1, 1).CreateMesh();

            for (int k = 0; k < ArraySizeY; k++)
            {
                for (int i = 0; i < ArraySizeX; i++)
                {
                    for (int j = 0; j < ArraySizeZ; j++)
                    {
                        // using motionstate is recommended, it provides interpolation capabilities and only synchronizes 'active' objects
                        //rbInfo.MotionState = new DefaultMotionState(startTransform);
                        entity = new Entity();
                        entity.Coordinates.LocalPosition = start + new Vector3(i, k, j);
                        physicsComponent = new PhysicsComponent(entity);
                        entity.ComponentManager.Components.Add(physicsComponent);
                        physicsComponent.PhysicsType = PhysicsType.Dynamic;
                        physicsComponent.Shape = new Box { Size = Vector3.One };
                        physicsComponent.Mass = mass;
                        meshComponent = new StaticMeshComponent(entity);
                        entity.ComponentManager.Components.Add(meshComponent);
                        meshComponent.Mesh = boxPrimitive;
                        meshComponent.Mesh.Initialize(GraphicsDevice);
                        meshComponent.Mesh.Texture = new Texture(GraphicsDevice, @"Content\paper_box_texture.jpg", GameManager.AssetContentManager);
                        world.AddEntityImmediately(entity);
                    }
                }
            }

            PhysicsDebugViewRendererComponent.DisplayPhysics = true;

            base.LoadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                Exit();
            }

            if (Keyboard.GetState().IsKeyDown(Keys.Add))
            {
            }

            if (Keyboard.GetState().IsKeyDown(Keys.Subtract))
            {
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}