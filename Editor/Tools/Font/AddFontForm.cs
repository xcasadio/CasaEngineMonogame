
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CasaEngine.Asset.Fonts;
using System.IO;

namespace Editor.Tools.Font
{
    public partial class AddFontForm : Form
    {
        /// <summary>
        /// 
        /// </summary>
        public string BmFile
        {
            get { return textBoxBmFile.Text; }
        }

        /// <summary>
        /// 
        /// </summary>
        public string PackageFile
        {
            get { return textBoxProjectPath.Text; }
            set { textBoxProjectPath.Text = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        public AddFontForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxProjectPath.Text) == false
                && string.IsNullOrWhiteSpace(textBoxBmFile.Text) == false)
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            DialogResult = DialogResult.None;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonBmFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog form = new OpenFileDialog();
            form.Filter = "Font Files (.fnt)|*.fnt|All Files (*.*)|*.*";
            form.CheckFileExists = true;
            form.Multiselect = false;

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                textBoxBmFile.Text = form.FileName;
            }
        }
    }
}
