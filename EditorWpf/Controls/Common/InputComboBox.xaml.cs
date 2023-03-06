using System.Collections;
using System.Collections.Generic;
using System.Windows;

namespace EditorWpf.Controls.Common
{
    public partial class InputComboBox : Window
    {
        public string Description
        {
            set => LabelDescription.Content = value;
        }

        public string? SelectedItem
        {
            get => ComboBoxInput.SelectedItem as string;
            set => ComboBoxInput.SelectedItem = value;
        }

        public IList<string> Items
        {
            set => ComboBoxInput.ItemsSource = value;
        }

        public InputComboBox()
        {
            InitializeComponent();
        }

        public InputComboBox(Window owner)
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
