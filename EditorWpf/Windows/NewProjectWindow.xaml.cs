using System.Windows;
using System.Windows.Forms;

namespace EditorWpf.Windows
{
    public partial class NewProjectWindow : Window
    {
        public NewProjectWindow()
        {
            InitializeComponent();
        }

        public string ProjectName { get; set; }
        public string ProjectPath { get; private set; }

        private void ButtonSetProjectFolder_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.AutoUpgradeEnabled = true;
            //dialog.RootFolder = Environment.SpecialFolder.ApplicationData;
            //dialog.InitialDirectory = "true";

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ProjectPath = dialog.SelectedPath;
            }
        }

        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
