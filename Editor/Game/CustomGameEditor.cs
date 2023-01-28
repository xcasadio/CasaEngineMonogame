
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngineCommon.Helper;
using System.Windows.Forms;
using XNAFinalEngine.UserInterface;

namespace Editor.Game
{
    internal class CustomGameEditor
        : CustomGame
    {
        CasaEngine.Game.GraphicsDeviceManager m_GraphicsDeviceManager;
        //SpriteBatch m_SpriteBatch;
        //Texture2D m_Texture2D;

        Grid2DComponent m_Grid2DComponent;

        /// <summary>
        /// 
        /// </summary>
        public UserInterfaceManager UIManager
        {
            get;
            private set;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="control_"></param>
        public CustomGameEditor(System.Windows.Forms.Control control_)
            : base(control_)
        {
            m_GraphicsDeviceManager = new CasaEngine.Game.GraphicsDeviceManager(this);
            m_GraphicsDeviceManager.PreferredBackBufferHeight = control_.Height;
            m_GraphicsDeviceManager.PreferredBackBufferWidth = control_.Width;

            m_Grid2DComponent = new Grid2DComponent(this);
            UIManager = new UserInterfaceManager();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void LoadContent()
        {
            base.LoadContent();
            UIManager.Initialize(m_GraphicsDeviceManager.GraphicsDevice, GameWindowHandle,
                new Microsoft.Xna.Framework.Rectangle(0, 0, GameWindow.ClientSize.Width, GameWindow.ClientSize.Height));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Update(GameTime gameTime)
        {
            UIManager.Update(GameTimeHelper.GameTimeToMilliseconds(gameTime));
            base.Update(gameTime);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Draw(GameTime gameTime)
        {
            UIManager.PreRenderControls();
            GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);
            base.Draw(gameTime);
            UIManager.RenderUserInterfaceToScreen();
        }
    }
}
