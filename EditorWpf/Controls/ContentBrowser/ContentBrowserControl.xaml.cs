using System.Windows.Controls;

namespace EditorWpf.Controls.ContentBrowser
{
    public partial class ContentBrowserControl : UserControl
    {
        public ContentBrowserControl()
        {
            DataContext = new ContentBrowserViewModel();
            InitializeComponent();
        }
    }
}
