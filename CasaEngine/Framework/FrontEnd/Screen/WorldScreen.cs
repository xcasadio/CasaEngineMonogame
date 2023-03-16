using CasaEngine.Framework.Graphics2D;

namespace CasaEngine.Framework.FrontEnd.Screen
{
    public class WorldScreen : Screen
    {
        private readonly World.World _world;

        public WorldScreen(World.World world, string worldName)
            : base(worldName)
        {
            if (world == null)
            {
                throw new ArgumentException("WorldScreen() : World is null");
            }

            TransitionOnTime = TimeSpan.FromSeconds(1.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);

            _world = world;
        }

        public override void LoadContent(Renderer2DComponent r)
        {
            base.LoadContent(r);
            // once the load has finished, we use ResetElapsedTime to tell the game's
            // timing mechanism that we have just finished a very long frame, and that
            // it should not try to catch up.
            Game.EngineComponents.Game.ResetElapsedTime();
        }

        public override void UnloadContent()
        {
        }

        public override void Update(float elapsedTime, bool otherScreenHasFocus,
                                                       bool coveredByOtherScreen)
        {
            base.Update(elapsedTime, otherScreenHasFocus, coveredByOtherScreen);

            if (IsActive)
            {
                _world.Update(elapsedTime);
            }
        }

        public override void HandleInput(InputState input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }
        }

        public override void Draw(float elapsedTime)
        {
            // This game has a blue background. Why? Because!
            /*GameInfo.Instance.GraphicsDeviceManager.GraphicsDevice.Clear(ClearOptions.Target,
                                               Color.CornflowerBlue, 0, 0);*/

            //base.ScreenManager.GraphicsDevice.RenderState.AlphaBlendEnable = false;
            //base.ScreenManager.GraphicsDevice.RenderState.AlphaTestEnable = false;
            //base.ScreenManager.GraphicsDevice.RenderState.BlendFunction = BlendFunction.Add;
            //GameInfo.Instance.GraphicsDeviceManager.GraphicsDevice.RenderState.DepthBufferEnable = true;
            //base.ScreenManager.GraphicsDevice.RenderState.DepthBufferWriteEnable = true;
            //base.ScreenManager.GraphicsDevice.RenderState.DestinationBlend = Blend.Zero;
            //base.ScreenManager.GraphicsDevice.RenderState.SourceBlend = Blend.One;

            _world.Draw(elapsedTime);

            // If the game is transitioning on or off, fade it out to black.
            if (TransitionPosition > 0)
            {
                ScreenManagerComponent.FadeBackBufferToBlack(255 - TransitionAlpha);
            }
        }

    }
}
