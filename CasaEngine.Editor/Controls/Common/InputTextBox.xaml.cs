using System.Windows;

namespace CasaEngine.Editor.Controls.Common
{
    public partial class InputTextBox : Window
    {
        public string Description
        {
            set => LabelDescription.Content = value;
        }

        public string? Text
        {
            get => TextInput.Text as string;
            set => TextInput.Text = value;
        }

        public InputTextBox()
        {
            InitializeComponent();
        }

        public InputTextBox(Window owner)
        {
            InitializeComponent();
            Owner = owner;
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
