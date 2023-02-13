namespace Editor.Sprite2DEditor
{
    public partial class DelayFrameOperationForm : Form
    {
        public DelayFrameOperationForm()
        {
            InitializeComponent();

            comboBox1.SelectedIndex = 0;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public string Operation
        {
            get { return comboBox1.SelectedItem as string; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public float Value
        {
            get { return Convert.ToSingle(numericUpDown1.Value); }
        }
    }
}
