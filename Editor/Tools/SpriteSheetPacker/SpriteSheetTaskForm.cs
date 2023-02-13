namespace Editor.Sprite2DEditor.SpriteSheetPacker
{
    /// <summary>
    /// 
    /// </summary>
    public partial class SpriteSheetTaskForm : Form
    {
        SpriteSheetTask m_SpriteSheetTask;

        /// <summary>
        /// 
        /// </summary>
        public SpriteSheetTask SpriteSheetTask
        {
            get { return m_SpriteSheetTask; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="task"></param>
        public SpriteSheetTaskForm(SpriteSheetTask task = null)
        {
            InitializeComponent();

            if (task != null)
            {
                textBoxTaskName.Text = task.Name;

                foreach (SpriteSheetTask.SpriteSheetBuild b in task.Builds)
                {
                    AddBuild(b);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAddTask_Click(object sender, EventArgs e)
        {
            SpriteSheetPackerForm form = new SpriteSheetPackerForm(null, true);

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                AddBuild(form.SpriteSheetBuild);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="build_"></param>
        private void AddBuild(SpriteSheetTask.SpriteSheetBuild build_)
        {
            if (m_SpriteSheetTask == null)
            {
                m_SpriteSheetTask = new SpriteSheetTask();
            }

            ListViewItem item = new ListViewItem(
                new string[] { build_.SpriteSheetName, build_.DetectAnimations == true ? "X" : "", build_.Files.Count.ToString() });
            listViewTask.Items.Add(item);
            m_SpriteSheetTask.AddBuild(build_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonEditTask_Click(object sender, EventArgs e)
        {
            if (listViewTask.SelectedIndices.Count > 0
                && m_SpriteSheetTask != null)
            {
                int index = listViewTask.SelectedIndices[listViewTask.SelectedIndices.Count - 1];
                SpriteSheetPackerForm form = new SpriteSheetPackerForm(m_SpriteSheetTask.GetBuild(index), true);

                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    m_SpriteSheetTask.SetBuild(index, form.SpriteSheetBuild);
                    ListViewItem item = new ListViewItem(
                        new string[] {
                            form.SpriteSheetBuild.SpriteSheetName,
                            form.SpriteSheetBuild.DetectAnimations == true ? "X" : "",
                            form.SpriteSheetBuild.Files.Count.ToString() });
                    listViewTask.Items[index] = item;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDeleteTask_Click(object sender, EventArgs e)
        {
            if (listViewTask.SelectedIndices.Count > 0
                && m_SpriteSheetTask != null
                && MessageBox.Show(this, "Do you want to delete?", "Confirm deletion", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                List<int> l = new List<int>();

                foreach (int index in listViewTask.SelectedIndices)
                {
                    l.Add(index);
                }

                l.Sort();
                l.Reverse();

                foreach (int index in l)
                {
                    m_SpriteSheetTask.RemoveAt(index);
                    listViewTask.Items.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxTaskName.Text) == true)
            {
                MessageBox.Show(this, "Please enter a name", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (m_SpriteSheetTask == null
                || (m_SpriteSheetTask != null
                    && m_SpriteSheetTask.Count == 0))
            {
                MessageBox.Show(this, "Please add at least one sprite sheet", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (m_SpriteSheetTask == null)
            {
                m_SpriteSheetTask = new SpriteSheetTask();
            }

            m_SpriteSheetTask.Name = textBoxTaskName.Text;

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listViewTask_SelectedIndexChanged(object sender, EventArgs e)
        {
            buttonDeleteTask.Enabled = listViewTask.SelectedIndices.Count > 0;
            buttonEditTask.Enabled = buttonDeleteTask.Enabled;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listViewTask_DoubleClick(object sender, EventArgs e)
        {
            buttonEditTask.PerformClick();
        }
    }
}
