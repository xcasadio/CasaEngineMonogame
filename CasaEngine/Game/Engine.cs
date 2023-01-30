using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Graphics2D;
using CasaEngine.Gameplay.Actor;
using CasaEngine.CoreSystems.Game;
using CasaEngine.FrontEnd.Screen;
using CasaEngine.Gameplay;
using CasaEngine.Project;
using CasaEngine.Asset;
using XNAFinalEngine.UserInterface;

#if EDITOR
using CasaEngine.Editor.Tools;
using CasaEngine.Editor.Assets;

#endif


namespace CasaEngine.Game
{
    public class Engine
    {

        static readonly private Engine ms_Engine = new Engine();

        private Microsoft.Xna.Framework.Game m_Game;
        private readonly Asset2DManager m_Asset2DManager;
        private SpriteFont m_DefaultSpriteFont;
        private SpriteBatch m_SpriteBatch;
        private readonly ProjectConfig m_ProjectConfig;
        private readonly ObjectRegistry m_ObjectRegistry;
        private readonly ProjectManager m_ProjectManager;
        //private PackageManager m_PackageManager;
        private readonly ObjectManager m_ObjectManager;
        private readonly ScreenManager m_ScreenManager;
        private readonly UserInterfaceManager m_UIManager;

#if EDITOR
        private readonly ExternalToolManager m_ExternalToolManager;
        private readonly AssetManager m_AssetManager;
        private BasicEffect m_Effect;
#endif
        /*
        private List<string> m_Errors = new List<string>();
		private List<string> m_Warnings = new List<string>();
		private string[] m_Arguments = null;*/




        static public Engine Instance => ms_Engine;

        internal bool ResetDevice
        {
            get;
            set;
        }

        public GraphicsDeviceManager GraphicsDeviceManager => (GraphicsDeviceManager)GameHelper.GetService<IGraphicsDeviceManager>(m_Game);

        public GraphicsProfile GraphicsProfile => m_Game.GraphicsDevice.GraphicsProfile;

        public AssetContentManager AssetContentManager
        {
            get;
            internal set;
        }

        public Asset2DManager Asset2DManager => m_Asset2DManager;

        public ProjectConfig ProjectConfig => m_ProjectConfig;

        public ProjectManager ProjectManager => m_ProjectManager;

        /*public PackageManager PackageManager
        {
            get { return m_PackageManager; }
        }*/
        public ObjectManager ObjectManager => m_ObjectManager;

        public ScreenManager ScreenManager => m_ScreenManager;

        public UserInterfaceManager UIManager => m_UIManager;


        public int MultiSampleQuality { get; set; }


        public Microsoft.Xna.Framework.Game Game
        {
            get => m_Game;
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

        public SpriteFont DefaultSpriteFont
        {
            get => m_DefaultSpriteFont;
            set => m_DefaultSpriteFont = value;
        }

#endif

#if EDITOR

        public BasicEffect BasicEffect
        {
            get => m_Effect;
            set => m_Effect = value;
        }

        public AssetManager AssetManager => m_AssetManager;

        public ExternalToolManager ExternalToolManager => m_ExternalToolManager;

#endif

        public SpriteBatch SpriteBatch
        {
            get => m_SpriteBatch;
            set => m_SpriteBatch = value;
        }

        public ObjectRegistry ObjectRegistry => m_ObjectRegistry;

        /*public List<string> Errors
        {
            get { return m_Errors; }
        }

        public List<string> Warnings
        {
            get { return m_Warnings; }
        }*/

        public string[] Arguments
        {
            get;
            set;
        }



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



    }
}
