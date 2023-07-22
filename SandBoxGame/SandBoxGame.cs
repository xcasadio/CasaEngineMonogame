using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
            //new GridComponent(this);
            new AxisComponent(this);
            base.Initialize();

            //IsMouseVisible = true;

            //GameManager.SpriteRendererComponent.IsDrawCollisionsEnabled = true;
            //GameManager.SpriteRendererComponent.IsDrawSpriteOriginEnabled = true;

            //GameManager.Renderer2dComponent.IsDrawCollisionsEnabled = true;
            //GameManager.Renderer2dComponent.IsDrawSpriteOriginEnabled = true;

            //PhysicsDebugViewRendererComponent.DisplayPhysics = true;
        }

        protected override void LoadContent()
        {
            GameManager.DefaultSpriteFont = Content.Load<SpriteFont>("GizmoFont");

            var world = new World();
            GameManager.CurrentWorld = world;

            //============ Camera ===============
            var entity = new Entity();
            //var camera = new ArcBallCameraComponent(entity);
            //camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
            var camera = new Camera3dIn2dAxisComponent(entity);
            camera.Target = new Vector3(Window.ClientBounds.Size.X / 2f, Window.ClientBounds.Size.Y / 2f, 0.0f);
            entity.ComponentManager.Components.Add(camera);
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
            entity.Coordinates.LocalPosition = new Vector3(0, 700, 0.0f);
            var tiledMapComponent = new TiledMapComponent(entity);
            tiledMapComponent.TiledMapData = tiledMapData;
            entity.ComponentManager.Components.Add(tiledMapComponent);

            world.AddEntityImmediately(entity);

            //============ animated sprite ===============
            SpriteLoader.LoadFromFile("Content\\ryu.spritesheet", GameManager.AssetContentManager);
            var animations = Animation2dLoader.LoadFromFile("Content\\ryu.anims2d", GameManager.AssetContentManager);

            entity = new Entity();
            entity.Name = "Ryu 1";
            entity.Coordinates.LocalPosition = new Vector3(250, 300, 1.0f);
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
            entity.Name = "Ryu 2";
            entity.OnHit += Entity_OnHit;
            //entity.OnHitEnded += Entity_OnHitEnded;
            entity.Coordinates.LocalPosition = new Vector3(450, 300, 1.0f);
            animatedSprite = new AnimatedSpriteComponent(entity);
            entity.ComponentManager.Components.Add(animatedSprite);
            animatedSprite.AddAnimation(new Animation2d(animations.First(x => x.Name == "idle")));
            animatedSprite.SetCurrentAnimation("idle", true);
            world.AddEntityImmediately(entity);

            base.LoadContent();

            GameManager.ActiveCamera = camera;
            //_animatedSpriteComponent.SetCurrentAnimation("run_forward", true);
            _animatedSpriteComponent.SetCurrentAnimation("230", true);
        }

        private void Entity_OnHit(object sender, EventCollisionArgs e)
        {
            Debug.WriteLine($"OnHit {e.Collision.ColliderA.Owner.Name} {e.Collision.ColliderB.Owner.Name}");
        }
        private void Entity_OnHitEnded(object sender, EventCollisionArgs e)
        {
            Debug.WriteLine($"OnHitEnded {e.Collision.ColliderA.Owner.Name} {e.Collision.ColliderB.Owner.Name}");
        }

        private AnimatedSpriteComponent _animatedSpriteComponent;
        private int index = 0;
        private Texture2D _texture2D;


        private int _colorBlendFunctionIndex = 0;
        private int _alphaBlendFunctionIndex = 0;
        private int _colorSourceBlendIndex = 0;
        private int _alphaSourceBlendIndex = 0;
        private int _colorDestinationBlendIndex = 0;
        private int _alphaDestinationBlendIndex = 0;

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                Exit();
            }

            var millisecondsTimeout = 200;

            if (Keyboard.GetState().IsKeyDown(Keys.W))
            {
                var values = Enum.GetValues<BlendFunction>();
                _colorBlendFunctionIndex = _colorBlendFunctionIndex + 1 >= values.Length ? 0 : _colorBlendFunctionIndex + 1;

                GameManager.SpriteRendererComponent._blendState = new BlendState
                {
                    ColorSourceBlend = GameManager.SpriteRendererComponent._blendState.ColorSourceBlend,
                    AlphaSourceBlend = GameManager.SpriteRendererComponent._blendState.AlphaSourceBlend,
                    ColorDestinationBlend = GameManager.SpriteRendererComponent._blendState.ColorDestinationBlend,
                    AlphaDestinationBlend = GameManager.SpriteRendererComponent._blendState.AlphaDestinationBlend,
                    AlphaBlendFunction = GameManager.SpriteRendererComponent._blendState.ColorBlendFunction,
                    ColorBlendFunction = values[_colorBlendFunctionIndex]
                };

                Thread.Sleep(millisecondsTimeout);
            }
            if (Keyboard.GetState().IsKeyDown(Keys.X))
            {
                var values = Enum.GetValues<BlendFunction>();
                _alphaBlendFunctionIndex = _alphaBlendFunctionIndex + 1 >= values.Length ? 0 : _alphaBlendFunctionIndex + 1;

                GameManager.SpriteRendererComponent._blendState = new BlendState
                {
                    ColorSourceBlend = GameManager.SpriteRendererComponent._blendState.ColorSourceBlend,
                    AlphaSourceBlend = GameManager.SpriteRendererComponent._blendState.AlphaSourceBlend,
                    ColorDestinationBlend = GameManager.SpriteRendererComponent._blendState.ColorDestinationBlend,
                    AlphaDestinationBlend = GameManager.SpriteRendererComponent._blendState.AlphaDestinationBlend,
                    AlphaBlendFunction = values[_alphaBlendFunctionIndex],
                    ColorBlendFunction = GameManager.SpriteRendererComponent._blendState.ColorBlendFunction
                };

                Thread.Sleep(millisecondsTimeout);
            }
            if (Keyboard.GetState().IsKeyDown(Keys.C))
            {
                var values = Enum.GetValues<Blend>();
                _colorSourceBlendIndex = _colorSourceBlendIndex + 1 >= values.Length ? 0 : _colorSourceBlendIndex + 1;

                GameManager.SpriteRendererComponent._blendState = new BlendState
                {
                    ColorSourceBlend = values[_colorSourceBlendIndex],
                    AlphaSourceBlend = GameManager.SpriteRendererComponent._blendState.AlphaSourceBlend,
                    ColorDestinationBlend = GameManager.SpriteRendererComponent._blendState.ColorDestinationBlend,
                    AlphaDestinationBlend = GameManager.SpriteRendererComponent._blendState.AlphaDestinationBlend,
                    AlphaBlendFunction = GameManager.SpriteRendererComponent._blendState.AlphaBlendFunction,
                    ColorBlendFunction = GameManager.SpriteRendererComponent._blendState.ColorBlendFunction
                };
                Thread.Sleep(millisecondsTimeout);
            }
            if (Keyboard.GetState().IsKeyDown(Keys.V))
            {
                var values = Enum.GetValues<Blend>();
                _alphaSourceBlendIndex = _alphaSourceBlendIndex + 1 >= values.Length ? 0 : _alphaSourceBlendIndex + 1;

                GameManager.SpriteRendererComponent._blendState = new BlendState
                {
                    ColorSourceBlend = GameManager.SpriteRendererComponent._blendState.ColorSourceBlend,
                    AlphaSourceBlend = values[_alphaSourceBlendIndex],
                    ColorDestinationBlend = GameManager.SpriteRendererComponent._blendState.ColorDestinationBlend,
                    AlphaDestinationBlend = GameManager.SpriteRendererComponent._blendState.AlphaDestinationBlend,
                    AlphaBlendFunction = GameManager.SpriteRendererComponent._blendState.AlphaBlendFunction,
                    ColorBlendFunction = GameManager.SpriteRendererComponent._blendState.ColorBlendFunction
                };
                Thread.Sleep(millisecondsTimeout);
            }
            if (Keyboard.GetState().IsKeyDown(Keys.B))
            {
                var values = Enum.GetValues<Blend>();
                _colorDestinationBlendIndex = _colorDestinationBlendIndex + 1 >= values.Length ? 0 : _colorDestinationBlendIndex + 1;

                GameManager.SpriteRendererComponent._blendState = new BlendState
                {
                    ColorSourceBlend = GameManager.SpriteRendererComponent._blendState.ColorSourceBlend,
                    AlphaSourceBlend = GameManager.SpriteRendererComponent._blendState.AlphaSourceBlend,
                    ColorDestinationBlend = values[_colorDestinationBlendIndex],
                    AlphaDestinationBlend = GameManager.SpriteRendererComponent._blendState.AlphaDestinationBlend,
                    AlphaBlendFunction = GameManager.SpriteRendererComponent._blendState.AlphaBlendFunction,
                    ColorBlendFunction = GameManager.SpriteRendererComponent._blendState.ColorBlendFunction
                };
                Thread.Sleep(millisecondsTimeout);
            }
            if (Keyboard.GetState().IsKeyDown(Keys.N))
            {
                var values = Enum.GetValues<Blend>();
                _alphaDestinationBlendIndex = _alphaDestinationBlendIndex + 1 >= values.Length ? 0 : _alphaDestinationBlendIndex + 1;

                GameManager.SpriteRendererComponent._blendState = new BlendState
                {
                    ColorSourceBlend = GameManager.SpriteRendererComponent._blendState.ColorSourceBlend,
                    AlphaSourceBlend = GameManager.SpriteRendererComponent._blendState.AlphaSourceBlend,
                    ColorDestinationBlend = GameManager.SpriteRendererComponent._blendState.ColorDestinationBlend,
                    AlphaDestinationBlend = values[_alphaDestinationBlendIndex],
                    AlphaBlendFunction = GameManager.SpriteRendererComponent._blendState.AlphaBlendFunction,
                    ColorBlendFunction = GameManager.SpriteRendererComponent._blendState.ColorBlendFunction
                };
                Thread.Sleep(millisecondsTimeout);
            }


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
            }

            if (Keyboard.GetState().IsKeyDown(Keys.Subtract))
            {
                index = MathHelper.Max(index - 1, 0);
                _animatedSpriteComponent.SetCurrentAnimation(index, true);
            }

            //GameManager.SpriteRendererComponent.DrawSprite(_sprite, new Vector2(100, 100), 0f, Vector2.One, Color.White, 0.0f, SpriteEffects.None);
            //GameManager.Renderer2dComponent.DrawSprite(_sprite, _sprite.SpriteData, new Vector2(200, 568), 0f, Vector2.One, Color.White, 0.0f, SpriteEffects.None);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            var blendState = GameManager.SpriteRendererComponent._blendState;
            var message =
                $"{blendState.ColorBlendFunction} - {blendState.AlphaBlendFunction} - {blendState.ColorSourceBlend} - {blendState.AlphaSourceBlend} - {blendState.ColorDestinationBlend} - {blendState.AlphaDestinationBlend}";

            GameManager.Renderer2dComponent.DrawText(GameManager.DefaultSpriteFont, message, Vector2.One, 0, Vector2.One, Color.White, 0.1f);


            base.Draw(gameTime);
        }
    }
}