using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace Editor
{
    public partial class SubMainForm : Form
    {
        ContentBrowserForm m_ContentBrowserForm;

        public SubMainForm()
        {
            InitializeComponent();

            m_ContentBrowserForm = new ContentBrowserForm();
            m_ContentBrowserForm.Show(dockPanel1, DockState.Document);
        }
    }
}
