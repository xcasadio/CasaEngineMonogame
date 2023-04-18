using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace SandBoxGame
{
    public class SandBoxGame : CasaEngineGame
    {
        protected override void Initialize()
        {
            base.Initialize();

            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {
            var world = new World();
            GameInfo.Instance.CurrentWorld = world;

            var entity = new Entity();
            var camera = new ArcBallCameraComponent(entity);
            entity.ComponentManager.Components.Add(camera);
            GameInfo.Instance.ActiveCamera = camera;
            camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
            world.AddEntityImmediately(entity);

            entity = new Entity();
            //entity.Coordinates.LocalPosition += Vector3.Up * 0.5f;
            var meshComponent = new StaticMeshComponent(entity);
            entity.ComponentManager.Components.Add(meshComponent);
            meshComponent.Mesh = new BoxPrimitive(GraphicsDevice).CreateMesh();
            meshComponent.Mesh.Initialize(GraphicsDevice);
            meshComponent.Mesh.Texture = new CasaEngine.Framework.Assets.Textures.Texture(
                GraphicsDevice, "Content\\checkboard.png", GameManager.AssetContentManager);
            world.AddEntityImmediately(entity);

            SpriteLoader.LoadFromFile(@"Content\TiledMap\Outside_A2_sprites.json", GameManager.AssetContentManager);
            TiledMapCreator.LoadMapFromFile(@"Content\TiledMap\tileMap.json", world, GameManager.AssetContentManager);

            base.LoadContent();
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
            base.Draw(gameTime);
        }
    }
}