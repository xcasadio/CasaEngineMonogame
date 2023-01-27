
#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Graphics2D;
using CasaEngine.Gameplay.Actor;
using CasaEngine;
using CasaEngine.World;
using CasaEngine.CoreSystems.Game;
using CasaEngine.FrontEnd.Screen;
using CasaEngine.Gameplay;
using CasaEngine.Project;
using CasaEngine.Asset;
using XNAFinalEngine.UserInterface;

#if EDITOR

using CasaEngine.Editor;
using CasaEngine.Editor.Tools;
using CasaEngine.Editor.Assets;

#endif

#endregion

namespace CasaEngine.Game
{
	/// <summary>
	/// 
	/// </summary>
	public class Engine
	{
		#region Fields

        static readonly private Engine ms_Engine = new Engine();

        private Microsoft.Xna.Framework.Game m_Game;
		private Asset2DManager m_Asset2DManager;        
		private SpriteFont m_DefaultSpriteFont;
		private SpriteBatch m_SpriteBatch;
        private ProjectConfig m_ProjectConfig;
        private ObjectRegistry m_ObjectRegistry;
        private ProjectManager m_ProjectManager;
        //private PackageManager m_PackageManager;
        private ObjectManager m_ObjectManager;
        private ScreenManager m_ScreenManager;
        private UserInterfaceManager m_UIManager;

#if EDITOR
        private ExternalToolManager m_ExternalToolManager;
        private AssetManager m_AssetManager;
        private BasicEffect m_Effect;
#endif
        /*
        private List<string> m_Errors = new List<string>();
		private List<string> m_Warnings = new List<string>();
		private string[] m_Arguments = null;*/
        
		
		#endregion

		#region Properties

        /// <summary>
        /// Gets
        /// </summary>
        static public Engine Instance
        {
            get { return ms_Engine; }
        }

        /// <summary>
        /// 
        /// </summary>
        internal bool ResetDevice
        { 
            get; 
            set; 
        }

        /// <summary>
		/// Gets
		/// </summary>
		public GraphicsDeviceManager GraphicsDeviceManager
		{
			get { return (GraphicsDeviceManager)GameHelper.GetService<IGraphicsDeviceManager>(m_Game); }
		}

        /// <summary>
        /// Gets
        /// </summary>
        public GraphicsProfile GraphicsProfile
        {
            get { return m_Game.GraphicsDevice.GraphicsProfile; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public AssetContentManager AssetContentManager
        {
            get;
            internal set;
        }

		/// <summary>
		/// Gets
		/// </summary>
		public Asset2DManager Asset2DManager
		{
			get { return m_Asset2DManager; }
		}

        /// <summary>
		/// Gets
		/// </summary>
		public ProjectConfig ProjectConfig
		{
			get { return m_ProjectConfig; }
		}

        /// <summary>
		/// Gets
		/// </summary>
        public ProjectManager ProjectManager
		{
            get { return m_ProjectManager; }
		}

        /// <summary>
        /// Gets
        /// </summary>
        /*public PackageManager PackageManager
        {
            get { return m_PackageManager; }
        }*/
        public ObjectManager ObjectManager
        {
            get { return m_ObjectManager; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public ScreenManager ScreenManager
        {
            get { return m_ScreenManager; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public UserInterfaceManager UIManager
        {
            get { return m_UIManager; }
        }

        #region Multi Sample Quality

        /// <summary>
        /// System Multi Sample Quality.
        /// Because the back buffer will be used only for 2D operations this value won’t be affect the back buffer.
        /// It's the level of multisampling, in this case 4 means 4X, and 0 means no multisampling.
        /// </summary>
        public int MultiSampleQuality { get; set; }

        #endregion

		/// <summary>
		/// Gets/Sets Game
		/// </summary>
		public Microsoft.Xna.Framework.Game Game
		{
			get { return m_Game; }
			set 
			{
				if (m_Game != null)
				{
					throw new InvalidOperationException("GameInfo.Instance.Game : Game is already set!");
				}

				m_Game = value;
			}
		}

#if !FINAL

		/// <summary>
		/// Gets/Sets
		/// </summary>
		public SpriteFont DefaultSpriteFont
		{
			get { return m_DefaultSpriteFont; }
			set { m_DefaultSpriteFont = value; }
		}

#endif

#if EDITOR

        /// <summary>
		/// Gets/Sets
		/// </summary>
		public BasicEffect BasicEffect
		{
			get { return m_Effect; }
			set { m_Effect = value; }
		}

                /// <summary>
        /// Gets
        /// </summary>
        public AssetManager AssetManager
        {
            get { return m_AssetManager; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public ExternalToolManager ExternalToolManager
        {
            get { return m_ExternalToolManager; }
        }
	
#endif

        /// <summary>
		/// Gets/Sets
		/// </summary>
		public SpriteBatch SpriteBatch
		{
			get { return m_SpriteBatch; }
			set { m_SpriteBatch = value; }
		}

        /// <summary>
        /// Gets
        /// </summary>
        public ObjectRegistry ObjectRegistry
        {
            get { return m_ObjectRegistry; }
        }

		/// <summary>
		/// Gets
		/// </summary>
        /*public List<string> Errors
        {
            get { return m_Errors; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public List<string> Warnings
        {
            get { return m_Warnings; }
        }*/

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public string[] Arguments
        {
            get;
            set;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public Engine()
        {
            m_Game = null;
            m_Asset2DManager = new Asset2DManager();
            m_DefaultSpriteFont = null;
            m_SpriteBatch = null;
            m_ProjectConfig = new ProjectConfig();
            m_ObjectRegistry = new ObjectRegistry();
            m_ProjectManager = new ProjectManager();
            //m_PackageManager = new PackageManager(m_ProjectManager);
            m_ObjectManager = new ObjectManager();
            m_ScreenManager = new ScreenManager();

            m_UIManager = new UserInterfaceManager();

#if EDITOR
            m_ExternalToolManager = new ExternalToolManager();
            m_AssetManager = new AssetManager();
            m_Effect = null;
#endif
        }

        #endregion

        #region Methods

        #endregion
    }
}
