#region File Description
//-----------------------------------------------------------------------------
// DebugManager.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

using CasaEngine.Game;
#if EDITOR
//using CasaEngine.Editor.GameComponent;
#endif

#endregion

namespace CasaEngine.Debugger
{
    /// <summary>
    /// DebugManager class that holds graphics resources for debug
    /// </summary>
    public class DebugManager
        : Microsoft.Xna.Framework.DrawableGameComponent
    {
        #region Properties

		/// <summary>
		/// Gets/Sets
		/// </summary>
		public bool DebugPickBufferTexture
		{
			get;
			set;
		}

		/// <summary>
		/// Gets/Sets
		/// </summary>
		public bool MakeScreenShot
		{
			get;
			set;
		}

        /// <summary>
        /// Gets white texture.
        /// </summary>
        public Texture2D WhiteTexture { get; private set; }

        #endregion

        #region Initialize

		/// <summary>
		/// 
		/// </summary>
		/// <param name="game"></param>
        public DebugManager(Microsoft.Xna.Framework.Game game)
            : base(game)
        {
            // Added as a Service.
            Game.Services.AddService(typeof(DebugManager), this);
            //Game.Components.Add(this);

            // This component doesn't need be call neither update nor draw.
            this.Enabled = false;
            this.Visible = false;

			/*UpdateOrder = (int)ComponentUpdateOrder.DebugManager;
			DrawOrder = (int)ComponentDrawOrder.DebugManager;*/

            VisibleChanged += new System.EventHandler<System.EventArgs>(DebugManager_VisibleChanged);
            EnabledChanged += new System.EventHandler<System.EventArgs>(DebugManager_EnabledChanged);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void DebugManager_EnabledChanged(object sender, System.EventArgs e)
        {
            this.Enabled = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void DebugManager_VisibleChanged(object sender, System.EventArgs e)
        {
            this.Visible = false;
        }

		/// <summary>
		/// 
		/// </summary>
		protected override void LoadContent()
		{
			// Load debug content.
			//SpriteBatch = new SpriteBatch(GraphicsDevice);

			//DebugFont = Game.Content.Load<SpriteFont>(debugFont);

			// Create white texture.
			WhiteTexture = new Texture2D(GraphicsDevice, 1, 1);
			Color[] whitePixels = new Color[] { Color.White };
			WhiteTexture.SetData<Color>(whitePixels);

			base.LoadContent();
		}

        #endregion
    }
}