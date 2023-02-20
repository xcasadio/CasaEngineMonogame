using CasaEngine.Core.Logger;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Editor.Tools
{
    class XnaEditorForm
    {
        Thread m_ThreadGame;
        Microsoft.Xna.Framework.Game m_Game;
        IEditorForm m_Form;
        EventWaitHandle m_EventWaitHandle;

        //use to restore Mouse.WindowHandle
        IntPtr m_XnaControlHandle;
        IntPtr m_PreviousHandle;

        public Thread ThreadGame
        {
            get { return m_ThreadGame; }
        }

        public Microsoft.Xna.Framework.Game Game
        {
            get { return m_Game; }
        }

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
            win.FormClosed += OnFormClosed;
            win.Disposed += OnFormDisposed;
            win.Activated += OnActivated;
            win.Deactivate += OnDeactivate;
            win.Resize += OnResize;
            m_Game = new Microsoft.Xna.Framework.Game(); //m_Form.XnaPanel.Handle, win.Width, win.Height);

            m_EventWaitHandle = new AutoResetEvent(true);

            Mouse.WindowHandle = m_XnaControlHandle;
        }

        private void OnResize(object? sender, EventArgs e)
        {
            if (m_Game.Services.GetService(typeof(IGraphicsDeviceService)) != null)
            {
                m_Game.GraphicsDevice.PresentationParameters.BackBufferWidth = m_Form.XnaPanel.Width;
                m_Game.GraphicsDevice.PresentationParameters.BackBufferHeight = m_Form.XnaPanel.Height;
                m_Game.GraphicsDevice.Reset();
                //m_Game.Resize(m_Form.XnaPanel.Width, m_Form.XnaPanel.Height);
            }
        }

        void OnDeactivate(object sender, EventArgs e)
        {
            Mouse.WindowHandle = m_PreviousHandle;
        }

        void OnActivated(object sender, EventArgs e)
        {
            m_PreviousHandle = Mouse.WindowHandle;
            Mouse.WindowHandle = m_XnaControlHandle;
        }

        public void StartGame()
        {
            m_ThreadGame = new Thread(g =>
            {
                try
                {
                    ((Microsoft.Xna.Framework.Game)g).Run();
                }
                catch (Exception ex)
                {
                    LogManager.Instance.WriteLineError("Exception in the game thread (" + ((Form)m_Form).Text + ")");
                    LogManager.Instance.WriteException(ex);
                }
            });
            m_ThreadGame.Start(m_Game);
        }

        private void Exit()
        {
            m_Game.Exit();
            //m_ThreadGame.Abort();
        }

        void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            Exit();
        }

        void OnFormDisposed(object sender, EventArgs e)
        {
            Exit();
        }
    }
}
