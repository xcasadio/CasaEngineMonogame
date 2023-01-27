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
    public partial class InputMultipleSelection : Form
    {
        /// <summary>
        /// 
        /// </summary>
        public string[] SelectedItems
        {
            get 
            { 
                string[] a = new string[listBox1.SelectedItems.Count];
                listBox1.SelectedItems.CopyTo(a, 0);
                return a; 
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public InputMultipleSelection()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="title_"></param>
        /// <param name="labelText_"></param>
        /// <param name="inputList_"></param>
        public InputMultipleSelection(string title_, string labelText_, string[] inputList_, bool multiSelection = false)
			: this()
		{
			this.Text = title_;
			label1.Text = labelText_;
            listBox1.Items.AddRange(inputList_);
            listBox1.SelectionMode = multiSelection == false ? SelectionMode.One : SelectionMode.MultiExtended;
		}
    }
}
