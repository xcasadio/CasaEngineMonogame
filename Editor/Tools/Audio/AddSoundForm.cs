
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Editor.Tools.Audio
{
    public partial class AddSoundForm : Form
    {
        /// <summary>
        /// 
        /// </summary>
        public string SoundFile
        {
            get { return textBoxFile.Text; }
        }

        /// <summary>
        /// 
        /// </summary>
        public string SoundPath
        {
            get { return textBoxPath.Text; }
        }

        /// <summary>
        /// 
        /// </summary>
        public AddSoundForm()
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

        }
    }
}
