using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using CasaEngine.Game;
using CasaEngine.Graphics2D;

namespace CasaEngine.FrontEnd.Screen
{
    public class WorldScreen
        : Screen
    {

        World.World m_World = null;



        /// <summary>
        /// Constructor.
        /// </summary>
        public WorldScreen(World.World world_, string worldName_)
            : base(worldName_)
        {
            if (world_ == null)
            {
                throw new ArgumentException("WorldScreen() : World is null");
            }

            TransitionOnTime = TimeSpan.FromSeconds(1.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);

            m_World = world_;
        }

        /// <summary>
        /// Load graphics content for the game.
        /// </summary>
        public override void LoadContent(Renderer2DComponent r_)
        {
            base.LoadContent(r_);
            // once the load has finished, we use ResetElapsedTime to tell the game's
            // timing mechanism that we have just finished a very long frame, and that
            // it should not try to catch up.
            Engine.Instance.Game.ResetElapsedTime();
        }

        /// <summary>
        /// Unload graphics content used by the game.
        /// </summary>
        public override void UnloadContent()
        {
        }



        /// <summary>
        /// Updates the state of the game. This method checks the Screen.IsActive
        /// property, so the game will stop updating when the pause menu is active,
        /// or if you tab away to a different application.
        /// </summary>
        public override void Update(float elapsedTime, bool otherScreenHasFocus,
                                                       bool coveredByOtherScreen)
        {
            base.Update(elapsedTime, otherScreenHasFocus, coveredByOtherScreen);

            if (IsActive)
            {
                m_World.Update(elapsedTime);
            }
        }

        /// <summary>
        /// Lets the game respond to player input. Unlike the Update method,
        /// this will only be called when the gameplay screen is active.
        /// </summary>
        public override void HandleInput(InputState input)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }
        }

        /// <summary>
        /// Draws the gameplay screen.
        /// </summary>
        public override void Draw(float elapsedTime_)
        {
            // This game has a blue background. Why? Because!
            /*GameInfo.Instance.GraphicsDeviceManager.GraphicsDevice.Clear(ClearOptions.Target,
                                               Color.CornflowerBlue, 0, 0);*/

            /////////////////////////////////////
            //base.ScreenManager.GraphicsDevice.RenderState.AlphaBlendEnable = false;
            //base.ScreenManager.GraphicsDevice.RenderState.AlphaTestEnable = false;
            //base.ScreenManager.GraphicsDevice.RenderState.BlendFunction = BlendFunction.Add;
            //GameInfo.Instance.GraphicsDeviceManager.GraphicsDevice.RenderState.DepthBufferEnable = true;
            //base.ScreenManager.GraphicsDevice.RenderState.DepthBufferWriteEnable = true;
            //base.ScreenManager.GraphicsDevice.RenderState.DestinationBlend = Blend.Zero;
            //base.ScreenManager.GraphicsDevice.RenderState.SourceBlend = Blend.One;

            m_World.Draw(elapsedTime_);

            // If the game is transitioning on or off, fade it out to black.
            if (TransitionPosition > 0)
                ScreenManagerComponent.FadeBackBufferToBlack(255 - TransitionAlpha);
        }

    }
}

