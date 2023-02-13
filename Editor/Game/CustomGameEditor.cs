using CasaEngine.Game;
using CasaEngine.UserInterface;
using Microsoft.Xna.Framework;
using CasaEngineCommon.Helper;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Editor.Game
{
    internal class CustomGameEditor
        : CustomGame
    {
        readonly CasaEngine.Game.GraphicsDeviceManager m_GraphicsDeviceManager;
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

        public CustomGameEditor(IntPtr handle, int width, int height)
            : base(handle)
        {
            m_GraphicsDeviceManager = new CasaEngine.Game.GraphicsDeviceManager(this);
            m_GraphicsDeviceManager.PreferredBackBufferHeight = width;
            m_GraphicsDeviceManager.PreferredBackBufferWidth = height;

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
                new Rectangle(0, 0,
                    m_GraphicsDeviceManager.GraphicsDevice.PresentationParameters.BackBufferWidth,
                    m_GraphicsDeviceManager.GraphicsDevice.PresentationParameters.BackBufferHeight));
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
