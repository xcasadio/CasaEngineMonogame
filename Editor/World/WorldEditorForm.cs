using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using Editor.Tools;
using CasaEngine.Editor.UndoRedo;
using CasaEngine.Game;
using System.Threading;

namespace Editor.World
{
    public partial class WorldEditorForm
        : DockContent, IEditorForm
    {

        XnaEditorForm m_XnaEditorForm;
        UndoRedoManager m_UndoRedoManager;


        /// <summary>
        /// 
        /// </summary>
        public Control XnaPanel
        {
            get { return panel1; }
        }

        /// <summary>
        /// 
        /// </summary>
        public WorldEditorForm()
        {
            InitializeComponent();

            m_UndoRedoManager = new UndoRedoManager();
            m_UndoRedoManager.EventCommandDone += new EventHandler(UndoRedoEventCommandDone);
            m_UndoRedoManager.UndoRedoCommandAdded += new EventHandler(UndoRedoCommandAdded);

            m_XnaEditorForm = new XnaEditorForm(this);
            //m_XnaEditorForm.Game.Content.RootDirectory = GameInfo.Instance.ProjectManager.ProjectPath;
            //Component creation
            // 
            Load += new EventHandler(OnFormLoad);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnFormLoad(object sender, EventArgs e)
        {
            m_XnaEditorForm.StartGame();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Thread t = new Thread(new ThreadStart(delegate { m_UndoRedoManager.Undo(); }));
            t.Start();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Thread t = new Thread(new ThreadStart(delegate { m_UndoRedoManager.Redo(); }));
            t.Start();
        }

        delegate void DefaultEventDelegate();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UndoRedoEventCommandDone(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new DefaultEventDelegate(OnUndoRedoEventCommandDone));
            }
            else
            {
                OnUndoRedoEventCommandDone();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnUndoRedoEventCommandDone()
        {
            /*undoToolStripMenuItem.Enabled = m_UndoRedoManager.CanUndo;
            redoToolStripMenuItem.Enabled = m_UndoRedoManager.CanRedo;*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UndoRedoCommandAdded(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new DefaultEventDelegate(OnUndoRedoCommandAdded));
            }
            else
            {
                OnUndoRedoCommandAdded();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnUndoRedoCommandAdded()
        {
            /*undoToolStripMenuItem.Enabled = m_UndoRedoManager.CanUndo;
            redoToolStripMenuItem.Enabled = m_UndoRedoManager.CanRedo;*/
        }

    }
}
