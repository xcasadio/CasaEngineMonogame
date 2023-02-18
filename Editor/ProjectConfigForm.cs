using System.Reflection;
using System.Diagnostics;
using CasaEngine.Framework.Game;

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

            propertyGrid1.SelectedObject = Engine.Instance.ProjectSettings;
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
