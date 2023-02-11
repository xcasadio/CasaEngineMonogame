using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Editor.WinForm
{
    public partial class ProgressBarForm : Form
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="title_"></param>
        /// <param name="label_"></param>
        /// <param name="min_"></param>
        /// <param name="max_"></param>
        public ProgressBarForm(string title_, string label_, int min_, int max_)
        {
            InitializeComponent();

            progressBar1.Minimum = min_;
            progressBar1.Maximum = max_;
            Text = title_;
            label.Text = label_;
            UpgradeLabelProgression();
        }

        /// <summary>
        /// Progression + 1
        /// </summary>
        public void ProgressionStep()
        {
            if (progressBar1.InvokeRequired == true)
            {
                MethodInvoker del = delegate { ProgressionStep(); };
                progressBar1.Invoke(del);
                return;
            }

            progressBar1.Value++;
            UpgradeLabelProgression();
        }

        /// <summary>
        /// 
        /// </summary>
        private void UpgradeLabelProgression()
        {
            if (labelProgression.InvokeRequired == true)
            {
                MethodInvoker del = delegate { UpgradeLabelProgression(); };
                labelProgression.Invoke(del);
                return;
            }

            labelProgression.Text = progressBar1.Minimum + "\\" + progressBar1.Maximum;
        }
    }
}
