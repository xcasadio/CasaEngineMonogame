using System;
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
            get => TextInput.Text;
            set => TextInput.Text = value;
        }

        public Func<string?, bool>? Predicate { get; set; }

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
            if (Predicate != null && !Predicate(Text))
            {
                return;
            }

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
