using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Game;
using XNAFinalEngine.Helpers;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.CoreSystems
{
    /// <summary>
    /// Used to get and set information about the screen and window.
    /// </summary>
    public class Screen
    {
        #region Properties

        #region Resolution

        /// <summary>
        /// Screen width.
        /// </summary>        
        public int Width { get { return GraphicsDevice.PresentationParameters.BackBufferWidth; } }

        /// <summary>
        /// Screen height.
        /// </summary>        
        public int Height { get { return GraphicsDevice.PresentationParameters.BackBufferHeight; } }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        private GraphicsDevice GraphicsDevice { get; set; }

        #endregion

        #region Events

        /// <summary>
        /// Raised when the window size changes.
        /// </summary>
        public event EventHandler ScreenSizeChanged;

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public Screen(GraphicsDevice graphicsDevice_)
        {
            GraphicsDevice = graphicsDevice_;
        }

        #endregion

        #region On Screen Size Changed

        /// <summary>
        /// Raised when the window size changes.
        /// </summary>
        internal void OnScreenSizeChanged(object sender, EventArgs e)
        {
            if (ScreenSizeChanged != null)
                ScreenSizeChanged(sender, EventArgs.Empty);
        } // OnScreenSizeChanged

        #endregion

    } // Screen

}
