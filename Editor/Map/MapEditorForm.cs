using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CasaEngine.Game;
using Editor.Game;
using System.Threading;
using CasaEngine.Graphics2D;

namespace Editor.Map
{
    /// <summary>
    /// 
    /// </summary>
    public partial class MapEditorForm 
        : Form
    {
        MyOwnEditorGame m_Game;
        Thread m_ThreadGame;

        /// <summary>
        /// 
        /// </summary>
        public MapEditorForm()
        {
            InitializeComponent();

            this.FormClosing += new FormClosingEventHandler(FormClosingCallback);
            this.Disposed += new EventHandler(Form_DisposedCallback);

            m_ThreadGame = new Thread(RunGame);
            m_ThreadGame.Start();      
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void FormClosingCallback(object sender, FormClosingEventArgs e)
        {
            Form_DisposedCallback(sender, EventArgs.Empty);                     
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Form_DisposedCallback(object sender, EventArgs e)
        {
            m_Game.Exit();
            //m_ThreadGame.Abort();
        }

        /// <summary>
        /// 
        /// </summary>
        private void RunGame()
        {
            m_Game = new MyOwnEditorGame();
            Renderer2DComponent r = new Renderer2DComponent(m_Game);
            m_Game.AttachedForm = this;
            m_Game.Run();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pictureBoxXNA_Resize(object sender, EventArgs e)
        {
            /*m_Game.GraphicsDeviceManager.PreferredBackBufferWidth = pictureBoxXNA.Width;
            m_Game.GraphicsDeviceManager.PreferredBackBufferHeight = pictureBoxXNA.Height;
            m_Game.GraphicsDeviceManager.ApplyChanges();*/
        }
    }
}
