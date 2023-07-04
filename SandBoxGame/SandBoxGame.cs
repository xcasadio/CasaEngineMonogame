using System.Linq;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Engine.Renderer;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.Graphics2D;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace SandBoxGame
{
    public class SandBoxGame : CasaEngineGame
    {
        protected override void Initialize()
        {
            new GridComponent(this);
            new AxisComponent(this);
            base.Initialize();

            IsMouseVisible = true;

            GameManager.SpriteRendererComponent.IsDrawCollisionsEnabled = true;
            GameManager.SpriteRendererComponent.IsDrawSpriteOriginEnabled = true;

            GameManager.Renderer2dComponent.IsDrawCollisionsEnabled = true;
            GameManager.Renderer2dComponent.IsDrawSpriteOriginEnabled = true;
        }

        protected override void LoadContent()
        {
            var world = new World();
            GameManager.CurrentWorld = world;

            //============ Camera ===============
            var entity = new Entity();
            var camera = new ArcBallCameraComponent(entity);
            camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
            //var camera = new Camera3dIn2dAxisComponent(entity);
            //camera.Target = new Vector3(Window.ClientBounds.Size.X / 2f, Window.ClientBounds.Size.Y / 2f, 0.0f);
            entity.ComponentManager.Components.Add(camera);
            GameManager.ActiveCamera = camera;
            world.AddEntityImmediately(entity);

            //============ Box ===============
            entity = new Entity();
            entity.Coordinates.LocalPosition = -Vector3.UnitZ * 5f;
            var meshComponent = new StaticMeshComponent(entity);
            entity.ComponentManager.Components.Add(meshComponent);
            meshComponent.Mesh = new BoxPrimitive(GraphicsDevice).CreateMesh();
            meshComponent.Mesh.Initialize(GraphicsDevice);
            meshComponent.Mesh.Texture = new Texture(GraphicsDevice, @"Content\checkboard.png", GameManager.AssetContentManager);
            //meshComponent.Mesh.Texture = GameManager.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName);
            world.AddEntityImmediately(entity);

            //============ tiledMap ===============
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
            var scale = 1.0f;
            entity.Coordinates.LocalScale = new Vector3(scale, scale, 1.0f);

            var animatedSprite = new AnimatedSpriteComponent(entity);
            entity.ComponentManager.Components.Add(animatedSprite);
            foreach (var animation in animations)
            {
                animatedSprite.AddAnimation(new Animation2d(animation));
            }

            _animatedSpriteComponent = animatedSprite;

            world.AddEntityImmediately(entity);

            //============ animated sprite 2 ===============
            entity = new Entity();
            entity.Coordinates.LocalPosition = new Vector3(450, 250, 0.0f);
            animatedSprite = new AnimatedSpriteComponent(entity);
            entity.ComponentManager.Components.Add(animatedSprite);
            animatedSprite.AddAnimation(new Animation2d(animations.First(x => x.Name == "idle")));
            animatedSprite.SetCurrentAnimation("idle", true);
            world.AddEntityImmediately(entity);


            PhysicsDebugViewRendererComponent.DisplayPhysics = true;

            //////**********************
            _spriteData = GameManager.AssetContentManager.GetAsset<SpriteData>("100_4");
            _sprite = Sprite.Create(_spriteData, GameManager.AssetContentManager);
            _texture2D = new Texture2D(GraphicsDevice, 2, 2, false, SurfaceFormat.Color);
            _texture2D.SetData(new Color[] { Color.Red, Color.Green, Color.Blue, Color.White });

            base.LoadContent();

            //_animatedSpriteComponent.SetCurrentAnimation("run_forward", true);
            _animatedSpriteComponent.SetCurrentAnimation("run_forward", true);
        }

        private AnimatedSpriteComponent _animatedSpriteComponent;
        private int index = 0;
        private SpriteData _spriteData;
        private Sprite _sprite;
        private Texture2D _texture2D;

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                Exit();
            }

            //if (Keyboard.GetState().IsKeyDown(Keys.Left))
            //{
            //    var camera = GameManager.ActiveCamera as Camera3dIn2dAxisComponent;
            //    var target = camera.Target;
            //    target.X -= 1;
            //    camera.Target = target;
            //}
            //if (Keyboard.GetState().IsKeyDown(Keys.Right))
            //{
            //    var camera = GameManager.ActiveCamera as Camera3dIn2dAxisComponent;
            //    var target = camera.Target;
            //    target.X += 1;
            //    camera.Target = target;
            //}
            //
            //if (Keyboard.GetState().IsKeyDown(Keys.Up))
            //{
            //    var camera = GameManager.ActiveCamera as Camera3dIn2dAxisComponent;
            //    var target = camera.Target;
            //    target.Y -= 1;
            //    camera.Target = target;
            //}
            //if (Keyboard.GetState().IsKeyDown(Keys.Down))
            //{
            //    var camera = GameManager.ActiveCamera as Camera3dIn2dAxisComponent;
            //    var target = camera.Target;
            //    target.Y += 1;
            //    camera.Target = target;
            //}


            if (Keyboard.GetState().IsKeyDown(Keys.Up))
            {
                var position = _animatedSpriteComponent.Owner.Coordinates.LocalPosition;
                position.Y -= 1;
                _animatedSpriteComponent.Owner.Coordinates.LocalPosition = position;
            }
            else if (Keyboard.GetState().IsKeyDown(Keys.Down))
            {
                var position = _animatedSpriteComponent.Owner.Coordinates.LocalPosition;
                position.Y += 1;
                _animatedSpriteComponent.Owner.Coordinates.LocalPosition = position;
            }
            if (Keyboard.GetState().IsKeyDown(Keys.Left))
            {
                var position = _animatedSpriteComponent.Owner.Coordinates.LocalPosition;
                position.X -= 1;
                _animatedSpriteComponent.Owner.Coordinates.LocalPosition = position;
            }
            else if (Keyboard.GetState().IsKeyDown(Keys.Right))
            {
                var position = _animatedSpriteComponent.Owner.Coordinates.LocalPosition;
                position.X += 1;
                _animatedSpriteComponent.Owner.Coordinates.LocalPosition = position;
            }

            if (Keyboard.GetState().IsKeyDown(Keys.Add))
            {
                index = MathHelper.Min(index + 1, _animatedSpriteComponent.Animations.Count - 1);
                _animatedSpriteComponent.SetCurrentAnimation(index, true);

                _spriteData = GameManager.AssetContentManager.GetAsset<SpriteData>(_animatedSpriteComponent.CurrentAnimation.CurrentFrame);
                _sprite = Sprite.Create(_spriteData, GameManager.AssetContentManager);
            }

            if (Keyboard.GetState().IsKeyDown(Keys.Subtract))
            {
                index = MathHelper.Max(index - 1, 0);
                _animatedSpriteComponent.SetCurrentAnimation(index, true);

                _spriteData = GameManager.AssetContentManager.GetAsset<SpriteData>(_animatedSpriteComponent.CurrentAnimation.CurrentFrame);
                _sprite = Sprite.Create(_spriteData, GameManager.AssetContentManager);
            }

            //GameManager.SpriteRendererComponent.DrawSprite(_sprite, new Vector2(100, 100), 0f, Vector2.One, Color.White, 0.0f, SpriteEffects.None);
            //GameManager.Renderer2dComponent.DrawSprite(_sprite, _sprite.SpriteData, new Vector2(200, 568), 0f, Vector2.One, Color.White, 0.0f, SpriteEffects.None);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}