using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CasaEngineCommon.Logger;
using Editor.Log;
using WeifenLuo.WinFormsUI.Docking;

namespace Editor.WinForm.Controls
{
    public partial class LogControl
        : Form
    {
        public LogControl()
        {
            InitializeComponent();

            try
            {
                LogManager.Instance.AddLogger(new LogEditor(this.richTextBox_Log));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Add Logger error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Dispose();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void richTextBox_Log_TextChanged(object sender, EventArgs e)
        {
            SuspendLayout();
            richTextBox_Log.SelectionStart = richTextBox_Log.Text.Length;
            richTextBox_Log.ScrollToCaret();
            richTextBox_Log.Refresh();
            ResumeLayout(true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonClearLog_Click(object sender, EventArgs e)
        {
            ClearLog();
        }

        /// <summary>
        /// Clear log
        /// </summary>
        public void ClearLog()
        {
            richTextBox_Log.Text = string.Empty;
        }
    }
}
