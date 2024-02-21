using System.Windows;

namespace CasaEngine.EditorUI.Windows
{
    public partial class Import3dFileOptionsWindow : Window
    {
        public bool ImportModel => importModelCheckBox.IsChecked ?? false;
        public bool ImportAnimations => importAnimationsCheckBox.IsChecked ?? false;
        public bool ImportTextures => importTexturesCheckBox.IsChecked ?? false;

        public Import3dFileOptionsWindow()
        {
            InitializeComponent();
        }

        private void ButtonCancelOnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ButtonOkOnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
