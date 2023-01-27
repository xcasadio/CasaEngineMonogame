using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Editor.Debugger
{
    public partial class DisplayExceptionForm : Form
    {
        public DisplayExceptionForm(string text)
        {
            InitializeComponent();

            textBox1.Text = text;
        }
    }
}
