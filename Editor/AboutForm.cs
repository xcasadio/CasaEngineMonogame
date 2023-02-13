namespace Editor
{
    /// <summary>
    /// 
    /// </summary>
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();

            label1.Text = "Project Editor - " + Application.ProductVersion;

#if DEBUG
            label1.Text += " - DEBUG";
#endif
        }
    }
}
