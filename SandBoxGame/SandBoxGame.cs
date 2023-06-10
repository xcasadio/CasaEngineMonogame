using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
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
            GameManager.CurrentWorld = world;

            //============ Camera ===============
            var entity = new Entity();
            var camera = new ArcBallCameraComponent(entity);
            entity.ComponentManager.Components.Add(camera);
            GameManager.ActiveCamera = camera;
            camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
            world.AddEntityImmediately(entity);

            entity = new Entity();
            //entity.Coordinates.LocalPosition += Vector3.Up * 0.5f;
            var meshComponent = new StaticMeshComponent(entity);
            entity.ComponentManager.Components.Add(meshComponent);
            meshComponent.Mesh = new BoxPrimitive(GraphicsDevice).CreateMesh();
            meshComponent.Mesh.Initialize(GraphicsDevice);
            meshComponent.Mesh.Texture = new Texture(GraphicsDevice, @"Content\checkboard.png", GameManager.AssetContentManager);
            //meshComponent.Mesh.Texture = GameManager.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName);
            world.AddEntityImmediately(entity);

            //============ tiledMap ===============
            //var tiledMapData = TiledMapLoader.LoadMapFromFile(@"Maps\map_1_1_tile_set.tiledMap");
            //
            //entity = new Entity();
            //var tiledMapComponent = new TiledMapComponent(entity);
            //tiledMapComponent.TiledMapData = tiledMapData;
            //entity.ComponentManager.Components.Add(tiledMapComponent);
            //
            //world.AddEntityImmediately(entity);

            //============ animated sprite ===============
            SpriteLoader.LoadFromFile("Content\\ryu.spritesheet", GameManager.AssetContentManager);
            var animations = Animation2dLoader.LoadFromFile("Content\\ryu.anims2d", GameManager.AssetContentManager);

            entity = new Entity();
            entity.Coordinates.LocalPosition = new Vector3(250, 250, 0.0f);
            var scale = 1.0f;
            entity.Coordinates.LocalScale = new Vector3(scale, scale, 1.0f);

            var animatedSprite = new AnimatedSpriteComponent(entity);
            entity.ComponentManager.Components.Add(animatedSprite);
            foreach (var animation in animations)
            {
                animatedSprite.AddAnimation(new Animation2d(animation));
            }

            //animatedSprite.SetCurrentAnimation(10, true);
            _animatedSpriteComponent = animatedSprite;

            world.AddEntityImmediately(entity);
            PhysicsDebugViewRendererComponent.DisplayPhysics = true;

            base.LoadContent();

            animatedSprite.SetCurrentAnimation("run_forward", true);
        }

        private AnimatedSpriteComponent _animatedSpriteComponent;
        private int index = 0;

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                Exit();
            }

            if (Keyboard.GetState().IsKeyDown(Keys.Add))
            {
                index = MathHelper.Min(index + 1, _animatedSpriteComponent.Animations.Count - 1);
                _animatedSpriteComponent.SetCurrentAnimation(index, true);
            }

            if (Keyboard.GetState().IsKeyDown(Keys.Subtract))
            {
                index = MathHelper.Max(index - 1, 0);
                _animatedSpriteComponent.SetCurrentAnimation(index, true);
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}