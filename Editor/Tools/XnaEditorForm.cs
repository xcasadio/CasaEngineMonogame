using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Editor.Game;
using CasaEngineCommon.Logger;

namespace Editor.Tools
{
    class XnaEditorForm
    {
        Thread m_ThreadGame;
        CustomGameEditor m_Game;
        IEditorForm m_Form;
        EventWaitHandle m_EventWaitHandle;

        //use to restore Mouse.WindowHandle
        IntPtr m_XnaControlHandle;
        IntPtr m_PreviousHandle;

        /// <summary>
        /// 
        /// </summary>
        public Thread ThreadGame
        {
            get { return m_ThreadGame; }
        }

        /// <summary>
        /// 
        /// </summary>
        public CustomGameEditor Game
        {
            get { return m_Game; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="form_"></param>
        public XnaEditorForm(IEditorForm form_)
        {
            if (form_ == null)
            {
                throw new ArgumentNullException("XnaEditorForm() : Form is null");
            }

            if (form_ is Form == false)
            {
                throw new ArgumentException("IEditorForm must be a System.Windows.Form");
            }

            m_Form = form_;
            Form win = (Form)m_Form;
            m_XnaControlHandle = m_Form.XnaPanel.Handle;
            win.FormClosed += new FormClosedEventHandler(OnFormClosed);
            win.Disposed += new EventHandler(OnFormDisposed);
            win.Activated += new System.EventHandler(this.OnActivated);
            win.Deactivate += new System.EventHandler(this.OnDeactivate);
            m_Game = new CustomGameEditor(m_Form.XnaPanel);

            m_EventWaitHandle = new AutoResetEvent(true);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnDeactivate(object sender, EventArgs e)
        {
            Microsoft.Xna.Framework.Input.Mouse.WindowHandle = m_PreviousHandle;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnActivated(object sender, EventArgs e)
        {
            m_PreviousHandle = Microsoft.Xna.Framework.Input.Mouse.WindowHandle;
            Microsoft.Xna.Framework.Input.Mouse.WindowHandle = m_XnaControlHandle;
        }


        /// <summary>
        /// 
        /// </summary>
        public void StartGame()
        {
            m_ThreadGame = new Thread(
                new ParameterizedThreadStart(delegate(object g)
                {
                    try
                    {
                        ((CustomGameEditor)g).Run();
                    }
                    catch (Exception ex)
                    {
                        LogManager.Instance.WriteLineError("Exception in the game thread (" + ((Form)m_Form).Text + ")");
                        LogManager.Instance.WriteException(ex);
                    }
                }));
            m_ThreadGame.Start(m_Game);
        }

        /// <summary>
        /// 
        /// </summary>
        private void Exit()
        {
            m_Game.Exit();
            //m_ThreadGame.Abort();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            Exit();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnFormDisposed(object sender, EventArgs e)
        {
            Exit();
        }
    }
}
