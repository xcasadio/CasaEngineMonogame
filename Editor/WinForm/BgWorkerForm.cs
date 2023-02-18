using System.ComponentModel;

namespace Editor.WinForm
{
    public partial class BgWorkerForm : Form
    {
        object m_Arg;

        /// <summary>
        /// Gets
        /// </summary>
        public BackgroundWorker BackgroundWorker
        {
            get { return backgroundWorker1; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public RunWorkerCompletedEventArgs Result
        {
            get;
            private set;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="delegate_"></param>
        /// <param name="arg"></param>
        public BgWorkerForm(DoWorkEventHandler delegate_, object arg = null)
        {
            InitializeComponent();

            m_Arg = arg;

            Shown += new EventHandler(BgWorkerForm_Shown);

            backgroundWorker1.WorkerReportsProgress = true;
            backgroundWorker1.DoWork += delegate_;//new DoWorkEventHandler(delegate_);
            backgroundWorker1.ProgressChanged += new ProgressChangedEventHandler(backgroundWorker1_ProgressChanged);
            backgroundWorker1.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker1_RunWorkerCompleted);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Result = e;
            Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void BgWorkerForm_Shown(object sender, EventArgs e)
        {
            buttonCancel.Visible = backgroundWorker1.WorkerSupportsCancellation;
            backgroundWorker1.RunWorkerAsync(m_Arg);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = System.Math.Min(progressBar1.Maximum, System.Math.Max(progressBar1.Minimum, e.ProgressPercentage));

            if (e.UserState is string)
            {
                label1.Text = e.UserState as string;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            try
            {
                backgroundWorker1.CancelAsync();
            }
            catch
            {
            }
        }
    }
}
