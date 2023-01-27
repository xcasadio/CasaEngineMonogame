using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Editor.WinForm;
using CasaEngineCommon.Logger;
using Editor.Sprite2DEditor.SpriteSheetPacker.sspack;
using Editor.WinForm.DocToolkit;

namespace Editor.Sprite2DEditor.SpriteSheetPacker
{
    public partial class SpriteSheetTaskListForm : Form
    {
        #region Fields

        private MruManager m_MruManager;
        private const string m_RegistryPath = "Software\\Studio_XC\\CasaEngine2DEditor\\SpriteSheetTaskList";

        static private string m_FileOpened = string.Empty;

        private SpriteSheetTaskManager m_SpriteSheetTaskManager = new SpriteSheetTaskManager();
        private SpriteSheetTaskManager m_OldSpriteSheetTaskManager;
        private EventHandler<BuildResultEventArgs> m_OnSpriteSheetBuildFinished;

        private BackgroundWorker m_BackgroundWorker;

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<BuildResultEventArgs> OnSpriteSheetBuildFinished
        {
            add
            {
                m_OnSpriteSheetBuildFinished += value;
            }
            remove
            {
                m_OnSpriteSheetBuildFinished -= value;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public SpriteSheetTaskListForm()
        {
            m_OldSpriteSheetTaskManager = null;
            InitializeComponent();

            m_MruManager = new MruManager();
            m_MruManager.Initialize(
                this,									// owner form
                toolStripMenuItemRecentProject,         // Recent Files menu item
                fileToolStripMenuItem,					// parent
                m_RegistryPath);						// Registry path to keep MRU list

            m_MruManager.MruOpenEvent += delegate(object sender_, MruFileOpenEventArgs e_)
            {
                LoadTaskFile(e_.FileName);
            };

#if !UNITEST
            if (m_MruManager.GetFirstFileName != null)
            {
                LoadTaskFile(m_MruManager.GetFirstFileName);
            }
#endif
        }

        #endregion

        #region Methods

        #region Static

        /// <summary>
        /// 
        /// </summary>
        static public void Clear()
        {
            m_FileOpened = string.Empty;
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAddTask_Click(object sender, EventArgs e)
        {
            SpriteSheetTaskForm form = new SpriteSheetTaskForm();

            if (form.ShowDialog() == DialogResult.OK)
            {
                AddTask(form.SpriteSheetTask);
            }

            form.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="task_"></param>
        private void AddTask(SpriteSheetTask task_, bool onlyInControls = false)
        {
            if (onlyInControls == false)
            {
                m_SpriteSheetTaskManager.AddTask(task_);
            }

            ListViewItem item = new ListViewItem(new string[] { task_.Name, task_.Count.ToString() });
            listView1.Items.Add(item);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDeleteTask_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0
                && MessageBox.Show(this, "Do you want to delete?", "Confirm deletion", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                List<int> l = new List<int>();

                foreach (int index in listView1.SelectedIndices)
                {
                    l.Add(index);
                }

                l.Sort();
                l.Reverse();

                foreach (int index in l)
                {
                    m_SpriteSheetTaskManager.RemoveTask(index);
                    listView1.Items.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index_"></param>
        /// <param name="task_"></param>
        private void UpdateTask(int index_, SpriteSheetTask task_)
        {
            ListViewItem item = new ListViewItem(new string[] { task_.Name, task_.Count.ToString() });
            listView1.Items[index_] = item;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonEditTask_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                SpriteSheetTask t = m_SpriteSheetTaskManager.GetTask(listView1.SelectedIndices[listView1.SelectedIndices.Count - 1]);
                
                SpriteSheetTaskForm form = new SpriteSheetTaskForm(t.Copy());
                if (form.ShowDialog() == DialogResult.OK
                    && t.Compare(form.SpriteSheetTask) == false)
                {
                    m_SpriteSheetTaskManager.SetTask(listView1.SelectedIndices[0], form.SpriteSheetTask);
                    UpdateTask(listView1.SelectedIndices[0], form.SpriteSheetTask);
                }
                
                form.Dispose();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonLaunch_Click(object sender, EventArgs e)
        {
            foreach (int index in listView1.CheckedIndices)
            {
                foreach (SpriteSheetTask.SpriteSheetBuild b in m_SpriteSheetTaskManager.GetTask(index).Builds)
                {
                    string error = CheckAllFilesExist(b.Files);

                    if (string.Empty.Equals(error) == true)
                    {
                        BgWorkerForm form = new BgWorkerForm(BuildSpriteSheet, b);
                        form.Text = "Build sprite sheet";
                        form.ShowDialog(this);
                        form.Dispose();
                    }
                    else
                    {
                        MessageBox.Show(error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        LogManager.Instance.WriteLineError(error);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="files_"></param>
        /// <returns>string.Empty if all files exists</returns>
        private string CheckAllFilesExist(IEnumerable<string> files_)
        {
            string res = string.Empty;

            foreach (string f in files_)
            {
                if (File.Exists(f) == false)
                {
                    res += "The file '" + f + "' doesn't exist." + Environment.NewLine;
                }
            }

            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BuildSpriteSheet(object sender, DoWorkEventArgs e)
        {
            m_BackgroundWorker = sender as BackgroundWorker;

            string mapFileName, spriteSheetFileName;
            SpriteSheetTask.SpriteSheetBuild build = (SpriteSheetTask.SpriteSheetBuild) e.Argument;

            Builder builder = new Builder();
            builder.ProgressChanged += new EventHandler<ProgressChangedEventArgs>(builder_ProgressChanged);
            int res = builder.Build(build, out spriteSheetFileName, out mapFileName);

            if (res == 0)
            {
                if (m_OnSpriteSheetBuildFinished != null)
                {
                    BuildResultEventArgs eventArgs = new BuildResultEventArgs(spriteSheetFileName, mapFileName, build.DetectAnimations);
                    m_OnSpriteSheetBuildFinished.Invoke(this, eventArgs);
                }

                try
                {
                    File.Delete(mapFileName);
                    File.Delete(spriteSheetFileName);
                }
                catch (Exception ex)
                {
                    LogManager.Instance.WriteException(ex);
                    MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                string msg = "Error packing images: " + Builder.ErrorCodeToString((FailCode)res);//SpaceErrorCode((sspack.FailCode)res);
                LogManager.Instance.WriteLineError(msg);
                MessageBox.Show(null, msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            m_BackgroundWorker = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void builder_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (m_BackgroundWorker != null)
            {
                m_BackgroundWorker.ReportProgress(e.ProgressPercentage, e.UserState);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="failCode"></param>
        /// <returns></returns>
        private static string SpaceErrorCode(Editor.Sprite2DEditor.SpriteSheetPacker.sspack.FailCode failCode)
        {
            string error = failCode.ToString();

            string result = error[0].ToString();

            for (int i = 1; i < error.Length; i++)
            {
                char c = error[i];
                if (char.IsUpper(c))
                    result += " ";
                result += c;
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(m_FileOpened) == false
                && m_OldSpriteSheetTaskManager != null
                && m_SpriteSheetTaskManager.Compare(m_OldSpriteSheetTaskManager) == false
                && MessageBox.Show(this, "Save changes ?", "Save", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                m_SpriteSheetTaskManager.Save(m_FileOpened);
            }

            this.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog form = new OpenFileDialog();
            form.Title = "Open task list";
            form.Filter = "Task list (*.xml)|*.xml|" +
                               "All Files (*.*)|*.*";

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                toolStripMenuItemNew_Click(null, EventArgs.Empty);
                LoadTaskFile(form.FileName);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        private void LoadTaskFile(string fileName_)
        {
            if (File.Exists(fileName_) == false)
            {
                m_MruManager.Remove(fileName_);
                //MessageBox.Show("The file " + fileName_ + " doesn't exists!");
                return;
            }

            m_FileOpened = fileName_;
            m_SpriteSheetTaskManager.Load(m_FileOpened);
            m_OldSpriteSheetTaskManager = m_SpriteSheetTaskManager;
            m_SpriteSheetTaskManager = m_SpriteSheetTaskManager.Copy();

            foreach (SpriteSheetTask t in m_SpriteSheetTaskManager.Tasks)
            {
                AddTask(t, true);
            }

            m_MruManager.Add(fileName_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (File.Exists(m_FileOpened) == true)
            {
                m_SpriteSheetTaskManager.Save(m_FileOpened);
            }
            else
            {
                toolStripMenuItem1_Click(sender, e);
            }            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SaveFileDialog form = new SaveFileDialog();
            form.Title = "Save task list";
            form.Filter = "Task list (*.xml)|*.xml|" +
                               "All Files (*.*)|*.*";

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                m_FileOpened = form.FileName;
                m_SpriteSheetTaskManager.Save(m_FileOpened);
            }
        }

        #endregion

        private void toolStripMenuItemNew_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();
            m_SpriteSheetTaskManager = new SpriteSheetTaskManager();
            m_OldSpriteSheetTaskManager = null;
            m_FileOpened = string.Empty;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            buttonDeleteTask.Enabled = listView1.SelectedIndices.Count > 0;
            buttonEditTask.Enabled = buttonDeleteTask.Enabled;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            buttonEditTask.PerformClick();
        }
    }
}
