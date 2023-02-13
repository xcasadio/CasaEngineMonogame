namespace Editor.WinForm
{
    public partial class InputRtfForm : Form
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="title"></param>
        /// <param name="rtfFileName"></param>
        public InputRtfForm(string title, string rtfFileName)
        {
            Text = title;

            InitializeComponent();

            try
            {
                richTextBox1.LoadFile(rtfFileName);
            }
            catch { }
        }
    }
}
