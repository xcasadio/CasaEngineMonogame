using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CasaEngine.Game;
using System.Reflection;
using System.Diagnostics;

namespace Editor
{
    public partial class ProjectConfigForm : Form
    {
        public ProjectConfigForm()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(asm.Location);

            Text = "Project Config - " + fvi.ProductVersion;

            InitializeComponent();

            propertyGrid1.SelectedObject = Engine.Instance.ProjectConfig;
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
