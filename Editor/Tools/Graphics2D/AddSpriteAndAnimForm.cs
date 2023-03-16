using CasaEngine.Framework.Game;

namespace Editor.Tools.Graphics2D
{
    public partial class AddSpriteAndAnimForm : Form
    {
        /// <summary>
        /// 
        /// </summary>
        public string PackageName
        {
            get { return textBoxPackage.Text; }
        }

        /// <summary>
        /// 
        /// </summary>
        public string[] Files
        {
            get
            {
                List<string> res = new List<string>();

                foreach (object obj in listBoxFiles.Items)
                {
                    res.Add(obj.ToString());
                }

                return res.ToArray();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool DetectAnimation2D
        {
            get { return checkBoxAnimations.Checked; }
        }

        /// <summary>
        /// 
        /// </summary>
        public AddSpriteAndAnimForm(string currentPath_)
        {
            InitializeComponent();

            textBoxPackage.Text = currentPath_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxPackage.Text) == true)
            {
                MessageBox.Show("Please set a package.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (listBoxFiles.Items.Count == 0)
            {
                MessageBox.Show("Please select a least one file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonRemoveSelected_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Do you want to remove selected files ?", "IsRemoved all", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                foreach (int index in listBoxFiles.SelectedIndices)
                {
                    listBoxFiles.Items.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonRemoveAll_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Do you want to remove all files ?", "IsRemoved all", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                listBoxFiles.Items.Clear();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDirectory_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog form = new FolderBrowserDialog();
            form.ShowNewFolderButton = false;
            form.Description = "select a directory";
            form.SelectedPath = EngineComponents.ProjectManager.ProjectPath;

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                foreach (string file in Directory.GetFiles(form.SelectedPath, "*.*", SearchOption.AllDirectories)
                                            .Where(s => s.EndsWith(".png") || s.EndsWith(".jpg") || s.EndsWith(".bmp") || s.EndsWith(".tga") || s.EndsWith(".jpeg")))
                {
                    listBoxFiles.Items.Add(file);
                }
            }

            form.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAddFiles_Click(object sender, EventArgs e)
        {
            OpenFileDialog form = new OpenFileDialog();
            form.CheckFileExists = true;
            form.Multiselect = true;

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                listBoxFiles.Items.AddRange(form.FileNames);
            }

            form.Dispose();
        }
    }
}
