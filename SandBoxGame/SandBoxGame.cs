using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.Textures;
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
            //SpriteLoader.LoadFromFile(@"Content\TiledMap\Outside_A2_sprites.json", GameManager.AssetContentManager);
            //var (tiledMapData, tileSetData, autoTileSetData) = TiledMapLoader.LoadMapFromFile(@"Content\TiledMap\tileMap.json");
            //SpriteLoader.LoadFromFile(tileSetData.SpriteSheetFileName, GameManager.AssetContentManager);
            //SpriteLoader.LoadFromFile(autoTileSetData.SpriteSheetFileName, GameManager.AssetContentManager);

            var tiledMapData = TiledMapLoader.LoadMapFromFile(@"Maps\map_1_1_tile_set.tiledMap");

            entity = new Entity();
            var tiledMapComponent = new TiledMapComponent(entity);
            tiledMapComponent.TiledMapData = tiledMapData;
            entity.ComponentManager.Components.Add(tiledMapComponent);

            world.AddEntityImmediately(entity);

            //============ animated sprite ===============
            SpriteLoader.LoadFromFile("Content\\ryu.spritesheet", GameManager.AssetContentManager);
            var animations = Animation2dLoader.LoadFromFile("Content\\ryu.anims2d", GameManager.AssetContentManager);

            entity = new Entity();
            entity.Coordinates.LocalPosition = new Vector3(250, 250, 0.0f);
            var scale = 2.0f;
            entity.Coordinates.LocalScale = new Vector3(scale, scale, 0.0f);

            var animatedSprite = new AnimatedSpriteComponent(entity);
            entity.ComponentManager.Components.Add(animatedSprite);
            foreach (var animation in animations)
            {
                animatedSprite.AddAnimation(new Animation2d(animation));
            }

            animatedSprite.SetCurrentAnimation("idle", true); //0, true);

            world.AddEntityImmediately(entity);




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