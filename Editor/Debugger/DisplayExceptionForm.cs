namespace Editor.Debugger
{
    public partial class DisplayExceptionForm : Form
    {
        public DisplayExceptionForm(string text)
        {
            InitializeComponent();

            textBox1.Text = text;
        }
    }
}
