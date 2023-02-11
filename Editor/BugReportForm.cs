
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Editor
{
    public partial class BugReportForm : Form
    {
        private Exception m_Exception;

        public BugReportForm(Exception ex)
        {
            if (ex == null)
            {
                throw new ArgumentNullException("BugReportForm : Exception is null");
            }

            m_Exception = ex;

            InitializeComponent();
        }

        private void buttonSend_Click(object sender, EventArgs e)
        {

        }

        private void buttonAttachFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog form = new OpenFileDialog();
            form.Filter = "*.*";
            form.Multiselect = false;
            form.Title = "Please select a file to attached";

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                textBoxAttachment.Text = form.FileName;
            }

            form.Dispose();
        }
    }
}
