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
	public partial class InputComboBox : Form
	{
		public string SelectedItem
		{
			get { return comboBoxInput.SelectedItem as string; }
		}

		public InputComboBox(string title_, string label_, string[] comboItem_)
		{
			if (comboItem_.Length == 0)
			{
				throw new ArgumentException("InputComboBox() : comboItem is empty");
			}

			InitializeComponent();

			this.Text = title_;
			labelText.Text = label_;

			foreach (string str in comboItem_)
			{
				comboBoxInput.Items.Add(str);
			}

			comboBoxInput.SelectedIndex = 0;
		}
	}
}
