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
