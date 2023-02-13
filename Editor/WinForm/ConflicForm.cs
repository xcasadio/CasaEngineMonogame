namespace Editor.WinForm
{
    public partial class ConflicForm : Form
    {
        /// <summary>
        /// 
        /// </summary>
        public enum ConflictAction
        {
            Replace,
            Skip,
            Rename
        }

        /// <summary>
        /// Gets
        /// </summary>
        public ConflictAction Action
        {
            get
            {
                if (radioButtonReplace.Checked == true)
                {
                    return ConflictAction.Replace;
                }

                if (radioButtonRename.Checked == true)
                {
                    return ConflictAction.Rename;
                }

                return ConflictAction.Skip;
            }
        }

        public ConflicForm(string fileName_)
        {
            InitializeComponent();

            label1.Text = "A file named '" + fileName_ + "' already exists." + Environment.NewLine + "What do you want to do?";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (radioButtonReplace.Checked == true
                || radioButtonRename.Checked == true
                || radioButtonSkip.Checked == true)
            {
                DialogResult = DialogResult.OK;
            }
            else
            {
                MessageBox.Show(this, "Please select an action.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Close();
        }
    }
}
