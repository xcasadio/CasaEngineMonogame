using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Editor.WinForm
{
    public partial class InputRtfForm : Form
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="title"></param>
        /// <param name="rtfFileName"></param>
        public InputRtfForm(string title, string rtfFileName)
        {
            this.Text = title;

            InitializeComponent();

            try
            {
                richTextBox1.LoadFile(rtfFileName);
            }
            catch { }
        }
    }
}
